#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright 2026 125hz
# Madeira Converter Exception: see LICENSE-EXCEPTION.md
"""Host checks for single-shot continuations in the Steam CM connection; never contacts Steam.

A CheckedContinuation resumed twice is a fatal error that takes down the app and
any running game. On a relaunch, connect() did that: URLSession called the
ping's pong handler a second time.

Part A is static: connect() waits for its pong only through
SteamConnection.awaitPong, and SteamSession's send-error and timeout paths
resume a job only after claiming it from its table (disconnect() resumes every
job it finds there first).

Part B compiles the production awaitPong and its guard with SteamError and
runs it: one call, error then error, success then error, a late second call,
and two calls racing on different threads. The first call wins, the rest are
traced, and nothing traps.
"""
from pathlib import Path
import os
import re
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[2]
steam = root / 'app/Madeira/SwiftSteam'
SWIFTC = os.environ.get('SWIFTC') or shutil.which('swiftc') or str(Path.home() / '.local/share/swiftly/bin/swiftc')
failures = 0


def require(condition, label):
    global failures
    print(('PASS: ' if condition else 'FAIL: ') + label, flush=True)
    if not condition:
        failures += 1


def block(source, start_marker):
    """Return the declaration starting at start_marker through its closing brace."""
    start = source.index(start_marker)
    depth, i = 0, source.index('{', start)
    while True:
        c = source[i]
        if c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0:
                return source[start:i + 1] + '\n'
        i += 1


# ---------------------------------------------------------------- Part A
connection = (steam / 'Core/SteamConnection.swift').read_text()
session = (steam / 'Core/SteamSession.swift').read_text()
connect = block(connection, 'func connect() async throws')
await_pong = block(connection, 'static func awaitPong(')
require('Self.awaitPong { task.sendPing(pongReceiveHandler: $0) }' in connect and 'withChecked' not in connect,
        'connect() waits for its pong only through awaitPong')
require(connection.count('withChecked') == 1 and 'withCheckedThrowingContinuation' in await_pong,
        "awaitPong holds SteamConnection's only continuation")
require('guard resumeOnce.claim() else {' in await_pong
        and await_pong.index('guard resumeOnce.claim()') < await_pong.index('cont.resume'),
        'awaitPong claims before it resumes')
require(re.search(r'^\s*pending(PICS)?Jobs\.removeValue\(forKey: jobID\)\s*$', session, re.M) is None,
        'SteamSession: a send error or timeout never drops a pending job without claiming it')
service = block(session, 'func callServiceMethod(')
require('if pendingJobs.removeValue(forKey: jobID) != nil {\n                        continuation.resume(throwing: error)' in service,
        'callServiceMethod: a send error resumes only a job still pending (not one disconnect() or the timeout resumed)')

# ---------------------------------------------------------------- Part B
guard = block(connection, 'private final class PongResumeGuard').replace('private final class', 'final class', 1)
stubs = r'''
import Foundation
enum SteamLog {
    static let lock = NSLock()
    nonisolated(unsafe) static var lines: [String] = []
    static func trace(_ m: @autoclosure () -> String) { let s = m(); lock.lock(); lines.append(s); lock.unlock() }
    static func event(_ m: String) {}
    static func take() -> [String] { lock.lock(); defer { lock.unlock() }; let l = lines; lines = []; return l }
}
''' + guard + 'enum SteamConnection {\n' + await_pong + '}\n'

checks = r'''
import Foundation
var failures = 0
func require(_ condition: @autoclosure () -> Bool, _ label: String) {
    if condition() { print("PASS: " + label) } else { print("FAIL: " + label); failures += 1 }
}
func err(_ text: String) -> Error { NSError(domain: "check", code: 1, userInfo: [NSLocalizedDescriptionKey: text]) }
func settled(_ group: DispatchGroup) async { await withCheckedContinuation { c in group.notify(queue: .global()) { c.resume() } } }
typealias Handler = @Sendable (Error?) -> Void

/// Run awaitPong with a stand-in for sendPing: .some(nil) when connected, .some(error) when it threw a SteamError.
func outcome(_ send: (@escaping Handler) -> Void) async -> SteamError?? {
    do { try await SteamConnection.awaitPong(send); return .some(nil) }
    catch let e as SteamError { return .some(e) }
    catch { return .none }
}

@main struct Checks {
    static func main() async {
        var r = await outcome { $0(nil) }
        require(r == .some(nil) && SteamLog.take().isEmpty, "one pong: connected, nothing traced")

        r = await outcome { $0(err("refused")) }
        require(r == .some(.connectionFailed("refused")), "one failure: connectionFailed with its text")

        r = await outcome { h in h(err("first")); h(err("second")) }
        var lines = SteamLog.take()
        require(r == .some(.connectionFailed("first")), "error then error: the first error is thrown, no trap")
        require(lines == ["WS pong handler called again (second): ignored"], "error then error: the repeat is traced (\(lines))")

        r = await outcome { h in h(nil); h(err("torn down")) }
        lines = SteamLog.take()
        require(r == .some(nil) && lines == ["WS pong handler called again (torn down): ignored"], "pong then teardown error: connected, repeat traced")

        // The second call lands after awaitPong has returned, as a teardown would.
        let late = DispatchGroup(); late.enter()
        r = await outcome { h in
            DispatchQueue.global().async { h(err("cancelled")) }
            DispatchQueue.global().asyncAfter(deadline: .now() + 0.05) { h(err("cancelled again")); late.leave() }
        }
        await settled(late)
        lines = SteamLog.take()
        require(r == .some(.connectionFailed("cancelled")) && lines == ["WS pong handler called again (cancelled again): ignored"],
                "a late second call after awaitPong returned is dropped")

        // Two calls racing on different threads: exactly one resume, every time.
        var wins = 0
        for _ in 0..<500 {
            let done = DispatchGroup(); done.enter()
            r = await outcome { h in
                DispatchQueue.global().async {
                    DispatchQueue.concurrentPerform(iterations: 2) { i in h(i == 0 ? nil : err("raced")) }
                    done.leave()
                }
            }
            await settled(done)
            if (r == .some(nil) || r == .some(.connectionFailed("raced"))) && SteamLog.take().count == 1 { wins += 1 }
        }
        require(wins == 500, "two racing calls: one resume and one trace in all 500 runs (\(wins))")

        if failures > 0 { print("FAILURES: \(failures)"); exit(1) }
        print("PASS: all Steam connection Swift checks")
    }
}
'''

with tempfile.TemporaryDirectory() as tmp:
    tmp = Path(tmp)
    (tmp / 'stubs.swift').write_text(stubs)
    (tmp / 'checks.swift').write_text(checks)
    sources = [tmp / 'stubs.swift', tmp / 'checks.swift', steam / 'Core/SteamError.swift']
    exe = tmp / 'swift-checks'
    build = subprocess.run([SWIFTC, '-parse-as-library', '-swift-version', '5', '-sanitize=address', '-o', str(exe)]
                           + [str(s) for s in sources])
    require(build.returncode == 0, 'production awaitPong compiles on the host')
    if build.returncode == 0:
        # A double resume traps; the Swift crash handler would otherwise hold the pipes for 30 s.
        run = subprocess.run([str(exe)], env=dict(os.environ, ASAN_OPTIONS='detect_leaks=0', SWIFT_BACKTRACE='enable=no'))
        require(run.returncode == 0, 'Swift checks pass under AddressSanitizer')

if failures:
    print(f'FAILURES: {failures}')
    sys.exit(1)
print('PASS: all Steam connection host checks')
