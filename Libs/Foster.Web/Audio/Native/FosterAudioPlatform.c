// Reuse the existing engine and codecs; replace only browser lifecycle and ABI.
#include "../../../Foster.Audio/Platform/src/foster_platform.c"
#include <emscripten.h>

static ma_resource_manager webResourceManager;
static ma_engine webEngine;
static long webJobTimer;

static void webLog(const char* message) { fprintf(stderr, "%s\n", message); }
static void webJobs(void* unused)
{
    // Single-threaded browser: process queued streaming jobs on the event loop.
    for (int i = 0; fstate.running && i < 64; ++i)
        if (ma_resource_manager_process_next_job(&webResourceManager) != MA_SUCCESS) break;
}

int FosterWebAudioStartup(void)
{
    if (fstate.running) return MA_SUCCESS;
    memset(&fstate, 0, sizeof(fstate));
    fstate.desc.onLogError = webLog;
    fstate.desc.onLogWarn = webLog;
    ma_resource_manager_config resources = ma_resource_manager_config_init();
    resources.flags = MA_RESOURCE_MANAGER_FLAG_NO_THREADING;
    resources.jobThreadCount = 0;
    resources.ppCustomDecodingBackendVTables = pCustomBackendVTables;
    resources.customDecodingBackendCount = sizeof(pCustomBackendVTables) / sizeof(pCustomBackendVTables[0]);
    ma_result result = ma_resource_manager_init(&resources, &webResourceManager);
    if (result != MA_SUCCESS) return result;
    ma_engine_config config = ma_engine_config_init();
    config.pResourceManager = &webResourceManager;
    result = ma_engine_init(&config, &webEngine);
    if (result != MA_SUCCESS) { ma_resource_manager_uninit(&webResourceManager); return result; }
    fstate.audioEngine = &webEngine;
    fstate.running = true;
    webJobTimer = emscripten_set_interval(webJobs, 10, NULL);
    return MA_SUCCESS;
}

void FosterWebAudioShutdown(void)
{
    if (!fstate.running) return;
    emscripten_clear_interval(webJobTimer);
    ma_engine_uninit(&webEngine);
    ma_resource_manager_uninit(&webResourceManager);
    fstate.audioEngine = NULL;
    fstate.running = false;
}

// Desktop declarations contain an unused index omitted by the managed API.
void FosterWebAudioSetVolume(float value) { FosterAudioSetVolume(0, value); }
void FosterWebAudioSetTimePcmFrames(uint64_t value) { FosterAudioSetTimePcmFrames(0, value); }

// WebAssembly P/Invoke uses scalar arguments rather than by-value structs.
void Web_FosterAudioListenerGetPosition(int index, float* x, float* y, float* z) { Vector3 v = FosterAudioListenerGetPosition(index); *x=v.x; *y=v.y; *z=v.z; }
void Web_FosterAudioListenerSetPosition(int index, float x, float y, float z) { Vector3 v = {x,y,z}; FosterAudioListenerSetPosition(index, v); }
void Web_FosterAudioListenerGetVelocity(int index, float* x, float* y, float* z) { Vector3 v = FosterAudioListenerGetVelocity(index); *x=v.x; *y=v.y; *z=v.z; }
void Web_FosterAudioListenerSetVelocity(int index, float x, float y, float z) { Vector3 v = {x,y,z}; FosterAudioListenerSetVelocity(index, v); }
void Web_FosterAudioListenerGetDirection(int index, float* x, float* y, float* z) { Vector3 v = FosterAudioListenerGetDirection(index); *x=v.x; *y=v.y; *z=v.z; }
void Web_FosterAudioListenerSetDirection(int index, float x, float y, float z) { Vector3 v = {x,y,z}; FosterAudioListenerSetDirection(index, v); }
void Web_FosterAudioListenerGetCone(int index, float* x, float* y, float* z) { FosterSoundCone v = FosterAudioListenerGetCone(index); *x=v.innerAngleInRadians; *y=v.outerAngleInRadians; *z=v.outerGain; }
void Web_FosterAudioListenerSetCone(int index, float x, float y, float z) { FosterSoundCone v = {x,y,z}; FosterAudioListenerSetCone(index, v); }
void Web_FosterAudioListenerGetWorldUp(int index, float* x, float* y, float* z) { Vector3 v = FosterAudioListenerGetWorldUp(index); *x=v.x; *y=v.y; *z=v.z; }
void Web_FosterAudioListenerSetWorldUp(int index, float x, float y, float z) { Vector3 v = {x,y,z}; FosterAudioListenerSetWorldUp(index, v); }
void Web_FosterSoundGetPosition(void* sound, float* x, float* y, float* z) { Vector3 v = FosterSoundGetPosition(sound); *x=v.x; *y=v.y; *z=v.z; }
void Web_FosterSoundSetPosition(void* sound, float x, float y, float z) { Vector3 v = {x,y,z}; FosterSoundSetPosition(sound, v); }
void Web_FosterSoundGetVelocity(void* sound, float* x, float* y, float* z) { Vector3 v = FosterSoundGetVelocity(sound); *x=v.x; *y=v.y; *z=v.z; }
void Web_FosterSoundSetVelocity(void* sound, float x, float y, float z) { Vector3 v = {x,y,z}; FosterSoundSetVelocity(sound, v); }
void Web_FosterSoundGetDirection(void* sound, float* x, float* y, float* z) { Vector3 v = FosterSoundGetDirection(sound); *x=v.x; *y=v.y; *z=v.z; }
void Web_FosterSoundSetDirection(void* sound, float x, float y, float z) { Vector3 v = {x,y,z}; FosterSoundSetDirection(sound, v); }
void Web_FosterSoundGetCone(void* sound, float* x, float* y, float* z) { FosterSoundCone v = FosterSoundGetCone(sound); *x=v.innerAngleInRadians; *y=v.outerAngleInRadians; *z=v.outerGain; }
void Web_FosterSoundSetCone(void* sound, float x, float y, float z) { FosterSoundCone v = {x,y,z}; FosterSoundSetCone(sound, v); }
