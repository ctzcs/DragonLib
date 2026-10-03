// qoaenc: 16-bit PCM WAV -> QOA (https://qoaformat.org), for shrinking Web builds (~1/5 of PCM size).
// Uses the qoa.h vendored with Foster.Audio, whose miniaudio backend decodes QOA on desktop and Web.
// Usage: qoaenc <input.wav> <output.qoa>   (build: build.ps1)
#define QOA_IMPLEMENTATION
#include "qoa.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static unsigned int u32(const unsigned char *p) { return p[0] | (p[1] << 8) | (p[2] << 16) | ((unsigned int)p[3] << 24); }
static unsigned int u16(const unsigned char *p) { return p[0] | (p[1] << 8); }

int main(int argc, char **argv)
{
    if (argc != 3) { fprintf(stderr, "usage: qoaenc <input.wav> <output.qoa>\n"); return 2; }
    FILE *f = fopen(argv[1], "rb");
    if (!f) { fprintf(stderr, "qoaenc: cannot open %s\n", argv[1]); return 1; }
    fseek(f, 0, SEEK_END); long size = ftell(f); fseek(f, 0, SEEK_SET);
    unsigned char *wav = malloc(size);
    if (!wav || fread(wav, 1, size, f) != (size_t)size) { fprintf(stderr, "qoaenc: cannot read %s\n", argv[1]); return 1; }
    fclose(f);
    if (size < 12 || memcmp(wav, "RIFF", 4) || memcmp(wav + 8, "WAVE", 4)) { fprintf(stderr, "qoaenc: %s is not a WAV file\n", argv[1]); return 1; }

    unsigned int channels = 0, rate = 0, bits = 0, format = 0, length = 0;
    const unsigned char *data = NULL;
    for (long p = 12; p + 8 <= size; ) {
        unsigned int chunk = u32(wav + p + 4);
        if (!memcmp(wav + p, "fmt ", 4) && chunk >= 16) {
            format = u16(wav + p + 8); channels = u16(wav + p + 10); rate = u32(wav + p + 12); bits = u16(wav + p + 22);
        } else if (!memcmp(wav + p, "data", 4)) {
            data = wav + p + 8; length = (p + 8 + (long)chunk <= size) ? chunk : (unsigned int)(size - p - 8);
        }
        p += 8 + chunk + (chunk & 1);
    }
    if (format != 1 || bits != 16 || !data || channels < 1 || channels > QOA_MAX_CHANNELS) {
        fprintf(stderr, "qoaenc: %s must be 16-bit PCM with 1-%d channels (format %u, %u bits, %u channels)\n",
            argv[1], QOA_MAX_CHANNELS, format, bits, channels);
        return 1;
    }
    qoa_desc desc = { .channels = channels, .samplerate = rate, .samples = length / (channels * 2) };
    unsigned int out_length = 0;
    void *encoded = qoa_encode((const short *)data, &desc, &out_length);
    if (!encoded) { fprintf(stderr, "qoaenc: encoding failed for %s\n", argv[1]); return 1; }
    FILE *o = fopen(argv[2], "wb");
    if (!o || fwrite(encoded, 1, out_length, o) != out_length) { fprintf(stderr, "qoaenc: cannot write %s\n", argv[2]); return 1; }
    fclose(o);
    return 0;
}
