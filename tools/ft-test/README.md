# ft-test

An x86-64 Windows console program. It checks that `setjmp`/`longjmp` work
(directly and across nested frames) and that FreeType renders glyphs, writing the
results to `C:\ft-test.txt`. The renders use `tahoma.ttf` at 8 to 200 px, with and
without embedded bitmaps; 200 px forces the rasteriser overflow path that calls
`longjmp`.

Under Madeira every check passed. This ruled out FreeType and SEH unwinding as
the cause of the invisible Unity text.

## Build

Build FreeType 2.13.3 statically for `x86_64-w64-mingw32`, then compile:

    x86_64-w64-mingw32-clang -O2 -I<freetype>/include ft-test.c libfreetype.a -o ft-test.exe -static
