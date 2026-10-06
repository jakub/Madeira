/* ft-test: minimal repro probe for "Unity renders no text under Madeira".
 * Exercises setjmp/longjmp (FreeType's gray rasterizer relies on them) and
 * FreeType outline + bitmap glyph rendering, writing results to C:\ft-test.txt. */
#include <setjmp.h>
#include <stdio.h>
#include <string.h>
#include <ft2build.h>
#include FT_FREETYPE_H

static FILE *out;
static jmp_buf jb;
static volatile int depth_marker;

static void deep_jump(int n)
{
    volatile char pad[256];
    memset((char *)pad, n, sizeof(pad));
    depth_marker = n;
    if (n == 0) longjmp(jb, 42);
    deep_jump(n - 1);
}

static void test_setjmp(void)
{
    volatile int local = 7;
    int r = setjmp(jb);
    if (r == 0)
    {
        fprintf(out, "setjmp first return 0 ok\n");
        longjmp(jb, 1);
    }
    fprintf(out, "simple longjmp returned %d (expect 1), local=%d (expect 7)\n", r, local);

    r = setjmp(jb);
    if (r == 0) deep_jump(8);
    fprintf(out, "nested longjmp returned %d (expect 42), depth_marker=%d (expect 0)\n", r, depth_marker);
}

static void render_size(FT_Face face, int px, FT_Int32 flags, const char *label)
{
    const char *text = "Hello World";
    int glyphs_ok = 0, total_lit = 0;
    FT_Error e = FT_Set_Pixel_Sizes(face, 0, px);
    if (e) { fprintf(out, "  %s px=%d FT_Set_Pixel_Sizes error %d\n", label, px, e); return; }
    for (const char *c = text; *c; c++)
    {
        e = FT_Load_Char(face, (unsigned char)*c, flags);
        if (e) { fprintf(out, "  %s px=%d '%c' FT_Load_Char error %d\n", label, px, *c, e); continue; }
        FT_Bitmap *bm = &face->glyph->bitmap;
        int lit = 0;
        for (unsigned y = 0; y < bm->rows; y++)
            for (unsigned x = 0; x < bm->width; x++)
                if (bm->buffer[y * bm->pitch + x]) lit++;
        total_lit += lit;
        if (lit || *c == ' ') glyphs_ok++;
        if (c == text || *c == 'W')
            fprintf(out, "  %s px=%d '%c' bitmap %ux%u pitch=%d mode=%d lit=%d advance=%ld format=%c%c%c%c\n",
                    label, px, *c, bm->width, bm->rows, bm->pitch, bm->pixel_mode, lit,
                    (long)(face->glyph->advance.x >> 6),
                    (char)(face->glyph->format >> 24), (char)(face->glyph->format >> 16),
                    (char)(face->glyph->format >> 8), (char)face->glyph->format);
    }
    fprintf(out, "  %s px=%d SUMMARY glyphs_with_pixels=%d/%d total_lit=%d\n",
            label, px, glyphs_ok, (int)strlen(text), total_lit);
}

int main(void)
{
    static const int sizes[] = { 8, 11, 13, 16, 24, 64, 200 };
    FT_Library lib;
    FT_Face face;
    FT_Error e;

    out = fopen("C:\\ft-test.txt", "w");
    if (!out) out = stdout;
    fprintf(out, "ft-test start\n");
    test_setjmp();

    e = FT_Init_FreeType(&lib);
    fprintf(out, "FT_Init_FreeType -> %d\n", e);
    if (e) goto done;
    e = FT_New_Face(lib, "C:\\windows\\Fonts\\tahoma.ttf", 0, &face);
    fprintf(out, "FT_New_Face tahoma -> %d", e);
    if (e) { fprintf(out, "\n"); goto done; }
    fprintf(out, " family=%s glyphs=%ld fixed_sizes=%d\n", face->family_name, face->num_glyphs, face->num_fixed_sizes);

    for (unsigned i = 0; i < sizeof(sizes) / sizeof(sizes[0]); i++)
    {
        render_size(face, sizes[i], FT_LOAD_RENDER, "default");
        render_size(face, sizes[i], FT_LOAD_RENDER | FT_LOAD_NO_BITMAP, "outline");
    }
    FT_Done_Face(face);
    FT_Done_FreeType(lib);
done:
    fprintf(out, "ft-test end\n");
    if (out != stdout) fclose(out);
    return 0;
}
