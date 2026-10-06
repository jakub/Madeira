// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 Jakub Burgis
// Madeira Converter Exception: see LICENSE-EXCEPTION.md

import Foundation

/// What Madeira installed from a game's Workshop collection, kept beside the
/// game's install records as `steamapps/workshop/madeira_<appid>.json`. Folders
/// are relative to `steamapps`, because the app's container path can change.
/// Only folders listed here are ever replaced or removed by a sync.
struct WorkshopRecord: Codable, Equatable {
    struct Entry: Codable, Equatable {
        var title: String
        /// The content manifest (decimal; JSON numbers lose 64-bit precision),
        /// "0" for a legacy single-file item.
        var manifest: String
        var timeUpdated: UInt32
        /// The installed folder, relative to `steamapps`.
        var folder: String
        /// The mod that required it, when no collection lists it.
        var requiredBy: UInt64?
        var bytes: UInt64
    }

    var appID: UInt32
    var collection: UInt64 = 0
    /// By Workshop item ID (decimal).
    var items: [String: Entry] = [:]

    static func url(appID: UInt32, steamApps: URL) -> URL {
        steamApps.appendingPathComponent("workshop/madeira_\(appID).json")
    }

    static func load(appID: UInt32, steamApps: URL) -> WorkshopRecord {
        guard let data = try? Data(contentsOf: url(appID: appID, steamApps: steamApps)),
              let record = try? JSONDecoder().decode(WorkshopRecord.self, from: data), record.appID == appID
        else { return WorkshopRecord(appID: appID) }
        return record
    }

    func save(steamApps: URL) throws {
        let url = Self.url(appID: appID, steamApps: steamApps)
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(self).write(to: url, options: .atomic)
    }
}

enum WorkshopInstall {
    /// Where an item's folder goes, relative to `steamapps`. A game that reads
    /// mods from its own folder gets them there (RimWorld: `Mods/<id>`, the only
    /// place it looks when Steam is not running); any other game gets Valve's
    /// layout, `workshop/content/<app>/<id>`.
    static func folder(itemID: UInt64, appID: UInt32, installFolder: String) -> String {
        switch appID {
        case 294100: return "common/\(installFolder)/Mods/\(itemID)"
        default: return "workshop/content/\(appID)/\(itemID)"
        }
    }

    struct Plan: Equatable {
        /// New items and items whose content changed, in the collection's order.
        var install: [WorkshopItem] = []
        /// Recorded items the collection no longer resolves to (item IDs).
        var remove: [String] = []
        var unchanged = 0
        var isEmpty: Bool { install.isEmpty && remove.isEmpty }
    }

    /// What a sync has to do: install what is new or changed (another manifest,
    /// a legacy item updated, or its folder gone), remove what was dropped.
    static func plan(_ resolution: SteamWorkshop.Resolution, record: WorkshopRecord,
                     folderExists: (String) -> Bool) -> Plan {
        var plan = Plan()
        let wanted = Set(resolution.mods.map { String($0.id) })
        for item in resolution.mods {
            if let entry = record.items[String(item.id)], entry.manifest == String(item.manifestID),
               item.manifestID != 0 || entry.timeUpdated == item.timeUpdated, folderExists(entry.folder) {
                plan.unchanged += 1
            } else {
                plan.install.append(item)
            }
        }
        plan.remove = record.items.keys.filter { !wanted.contains($0) }.sorted()
        return plan
    }

    /// A relative folder that stays inside `steamapps` (no `..`, not absolute).
    static func isContained(_ relative: String) -> Bool {
        !relative.isEmpty && !relative.hasPrefix("/") && !relative.split(separator: "/").contains("..")
    }

    /// The `<packageId>` an About/About.xml declares, lower-cased as RimWorld
    /// compares it, or nil.
    static func packageID(inModFolder folder: URL) -> String? {
        guard let data = try? Data(contentsOf: folder.appendingPathComponent("About/About.xml")),
              let text = String(data: data, encoding: .utf8) ?? String(data: data, encoding: .isoLatin1),
              let open = text.range(of: "<packageId>", options: .caseInsensitive),
              let close = text.range(of: "</packageId>", options: .caseInsensitive, range: open.upperBound..<text.endIndex)
        else { return nil }
        let id = text[open.upperBound..<close.lowerBound].trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return id.isEmpty ? nil : id
    }

    /// Folders beside `installed` (not installed by Madeira) that declare the
    /// same packageId: a second copy the game would load only one of.
    static func duplicates(of installed: URL, managed: Set<String>, steamApps: URL) -> [String] {
        guard let id = packageID(inModFolder: installed) else { return [] }
        let parent = installed.deletingLastPathComponent()
        let siblings = (try? FileManager.default.contentsOfDirectory(at: parent, includingPropertiesForKeys: [.isDirectoryKey])) ?? []
        let base = steamApps.standardizedFileURL.path + "/"
        return siblings.compactMap { url -> String? in
            guard url.lastPathComponent != installed.lastPathComponent,
                  (try? url.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true,
                  packageID(inModFolder: url) == id else { return nil }
            let path = url.standardizedFileURL.path
            let relative = path.hasPrefix(base) ? String(path.dropFirst(base.count)) : path
            return managed.contains(relative) ? nil : url.lastPathComponent
        }.sorted()
    }

    /// Outcome of a sync, for the log and the game's page.
    struct Result: Equatable {
        var installed = 0
        var removed = 0
        var unchanged = 0
        var bytes: UInt64 = 0
        /// "<title>: also installed by hand in Mods/<folder>; remove that copy".
        var warnings: [String] = []
    }

    /// Applies a plan: each item is downloaded to its staging folder by
    /// `download`, then swapped into place (the old copy is moved aside, the new
    /// one renamed in, the old one deleted), and the record is saved after every
    /// item so an interruption keeps what finished. Dropped items' recorded
    /// folders are deleted. `progress` reports items done of the total.
    static func apply(_ plan: Plan, resolution: SteamWorkshop.Resolution, record: inout WorkshopRecord,
                      installFolder: String, steamApps: URL,
                      download: (WorkshopItem) async throws -> (folder: URL, bytes: UInt64),
                      progress: (_ done: Int, _ total: Int, _ title: String) -> Void) async throws -> Result {
        let fm = FileManager.default
        var result = Result(unchanged: plan.unchanged)
        record.collection = resolution.collection.id
        let total = plan.install.count + plan.remove.count
        var done = 0

        for item in plan.install {
            try Task.checkCancellation()
            progress(done, total, item.title)
            let relative = folder(itemID: item.id, appID: record.appID, installFolder: installFolder)
            guard isContained(relative) else { continue }
            let staged = try await download(item)
            let destination = steamApps.appendingPathComponent(relative, isDirectory: true)
            try fm.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            let work = staged.folder.deletingLastPathComponent()
            let aside = work.appendingPathComponent("previous", isDirectory: true)
            try? fm.removeItem(at: aside)
            if fm.fileExists(atPath: destination.path) { try fm.moveItem(at: destination, to: aside) }
            try fm.moveItem(at: staged.folder, to: destination)
            try? fm.removeItem(at: work)
            record.items[String(item.id)] = WorkshopRecord.Entry(
                title: item.title, manifest: String(item.manifestID), timeUpdated: item.timeUpdated,
                folder: relative, requiredBy: resolution.requiredBy[item.id], bytes: staged.bytes)
            try record.save(steamApps: steamApps)
            result.installed += 1
            result.bytes += staged.bytes
            done += 1
            let managed = Set(record.items.values.map(\.folder))
            for copy in duplicates(of: destination, managed: managed, steamApps: steamApps) {
                result.warnings.append("\(item.title): also installed by hand in \(destination.deletingLastPathComponent().lastPathComponent)/\(copy); remove that copy")
            }
        }

        for id in plan.remove {
            try Task.checkCancellation()
            guard let entry = record.items[id] else { continue }
            progress(done, total, entry.title)
            if isContained(entry.folder) {
                try? fm.removeItem(at: steamApps.appendingPathComponent(entry.folder, isDirectory: true))
            }
            record.items[id] = nil
            try record.save(steamApps: steamApps)
            result.removed += 1
            done += 1
        }
        try record.save(steamApps: steamApps)
        progress(done, total, "")
        return result
    }
}
