// Web ABI adapter. Public audio types are source-linked from Foster.Audio.
using System.Numerics;
using System.Runtime.InteropServices;

namespace Foster.Audio;

internal static class Platform
{
	public const string DLL = "FosterAudioPlatform";

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterLogFn(IntPtr msg);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public struct FosterDesc
	{
		public FosterLogFn onLogInfo;
		public FosterLogFn onLogWarn;
		public FosterLogFn onLogError;
		public int logging;
	}

	public struct FosterBool
	{
		byte value;

		public FosterBool(byte v)
		{
			value = v;
		}

		public static implicit operator bool(FosterBool b) => b.value != 0;
		public static implicit operator FosterBool(bool b) => new(b ? (byte)1 : (byte)0);
	}

	[Flags]
	public enum FosterSoundFlags
	{
		STREAM = 0x00000001,
		DECODE = 0x00000002,
		NO_SPATIALIZATION = 0x00004000
	}

	[DllImport(DLL, EntryPoint = "FosterWebAudioStartup")]
	private static extern int StartupNative();
	public static void FosterAudioStartup(FosterDesc desc)
	{
		if (StartupNative() != 0)
			throw new InvalidOperationException("Unable to initialize the browser audio device.");
	}
	[DllImport(DLL, EntryPoint = "FosterWebAudioShutdown")]
	public static extern void FosterAudioShutdown();
	[DllImport(DLL)]
	public static extern float FosterAudioGetVolume();
	[DllImport(DLL, EntryPoint = "FosterWebAudioSetVolume")]
	public static extern void FosterAudioSetVolume(float value);
	[DllImport(DLL)]
	public static extern int FosterAudioGetChannels();
	[DllImport(DLL)]
	public static extern int FosterAudioGetSampleRate();
	[DllImport(DLL)]
	public static extern ulong FosterAudioGetTimePcmFrames();
	[DllImport(DLL, EntryPoint = "FosterWebAudioSetTimePcmFrames")]
	public static extern void FosterAudioSetTimePcmFrames(ulong value);
	[DllImport(DLL)]
	public static extern int FosterAudioGetListenerCount();
	[DllImport(DLL)]
	public static extern IntPtr FosterAudioDecode(IntPtr data, int length, ref AudioFormat format, ref int channels, ref int sampleRate, out ulong decodedFrameCount);
	[DllImport(DLL)]
	public static extern void FosterAudioFree(IntPtr data);
	[DllImport(DLL)]
	public static extern void FosterAudioRegisterEncodedData(string name, IntPtr data, int length);
	[DllImport(DLL)]
	public static extern void FosterAudioRegisterDecodedData(string name, IntPtr data, ulong frameCount, AudioFormat format, int channels, int sampleRate);
	[DllImport(DLL)]
	public static extern void FosterAudioUnregisterData(string name);

	[DllImport(DLL, EntryPoint = "FosterAudioListenerGetEnabled")]
	private static extern byte FosterAudioListenerGetEnabledNative(int index);
	public static FosterBool FosterAudioListenerGetEnabled(int index) => new(FosterAudioListenerGetEnabledNative(index));
	[DllImport(DLL, EntryPoint = "FosterAudioListenerSetEnabled")]
	private static extern void FosterAudioListenerSetEnabledNative(int index, byte value);
	public static void FosterAudioListenerSetEnabled(int index, FosterBool value) => FosterAudioListenerSetEnabledNative(index, value ? (byte)1 : (byte)0);
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerGetPosition")]
	private static extern void FosterAudioListenerGetPositionNative(int index, out float x, out float y, out float z);
	public static Vector3 FosterAudioListenerGetPosition(int index) { FosterAudioListenerGetPositionNative(index, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerSetPosition")]
	private static extern void FosterAudioListenerSetPositionNative(int index, float x, float y, float z);
	public static void FosterAudioListenerSetPosition(int index, Vector3 value) => FosterAudioListenerSetPositionNative(index, value.X, value.Y, value.Z);
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerGetVelocity")]
	private static extern void FosterAudioListenerGetVelocityNative(int index, out float x, out float y, out float z);
	public static Vector3 FosterAudioListenerGetVelocity(int index) { FosterAudioListenerGetVelocityNative(index, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerSetVelocity")]
	private static extern void FosterAudioListenerSetVelocityNative(int index, float x, float y, float z);
	public static void FosterAudioListenerSetVelocity(int index, Vector3 value) => FosterAudioListenerSetVelocityNative(index, value.X, value.Y, value.Z);
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerGetDirection")]
	private static extern void FosterAudioListenerGetDirectionNative(int index, out float x, out float y, out float z);
	public static Vector3 FosterAudioListenerGetDirection(int index) { FosterAudioListenerGetDirectionNative(index, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerSetDirection")]
	private static extern void FosterAudioListenerSetDirectionNative(int index, float x, float y, float z);
	public static void FosterAudioListenerSetDirection(int index, Vector3 value) => FosterAudioListenerSetDirectionNative(index, value.X, value.Y, value.Z);
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerGetCone")]
	private static extern void FosterAudioListenerGetConeNative(int index, out float x, out float y, out float z);
	public static SoundCone FosterAudioListenerGetCone(int index) { FosterAudioListenerGetConeNative(index, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerSetCone")]
	private static extern void FosterAudioListenerSetConeNative(int index, float x, float y, float z);
	public static void FosterAudioListenerSetCone(int index, SoundCone value) => FosterAudioListenerSetConeNative(index, value.InnerAngleInRadians, value.OuterAngleInRadians, value.OuterGain);
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerGetWorldUp")]
	private static extern void FosterAudioListenerGetWorldUpNative(int index, out float x, out float y, out float z);
	public static Vector3 FosterAudioListenerGetWorldUp(int index) { FosterAudioListenerGetWorldUpNative(index, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterAudioListenerSetWorldUp")]
	private static extern void FosterAudioListenerSetWorldUpNative(int index, float x, float y, float z);
	public static void FosterAudioListenerSetWorldUp(int index, Vector3 value) => FosterAudioListenerSetWorldUpNative(index, value.X, value.Y, value.Z);

	[DllImport(DLL)]
	public static extern IntPtr FosterSoundCreate(string path, FosterSoundFlags flags, IntPtr soundGroup);
	[DllImport(DLL)]
	public static extern void FosterSoundPlay(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundStop(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundDestroy(IntPtr sound);
	[DllImport(DLL)]
	public static extern float FosterSoundGetVolume(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetVolume(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetPitch(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPitch(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetPan(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPan(IntPtr sound, float value);
	[DllImport(DLL, EntryPoint = "FosterSoundGetPlaying")]
	private static extern byte FosterSoundGetPlayingNative(IntPtr sound);
	public static FosterBool FosterSoundGetPlaying(IntPtr sound) => new(FosterSoundGetPlayingNative(sound));
	[DllImport(DLL, EntryPoint = "FosterSoundGetFinished")]
	private static extern byte FosterSoundGetFinishedNative(IntPtr sound);
	public static FosterBool FosterSoundGetFinished(IntPtr sound) => new(FosterSoundGetFinishedNative(sound));
	[DllImport(DLL)]
	public static extern void FosterSoundGetDataFormat(IntPtr sound, out AudioFormat format, out int channels, out int sampleRate);
	[DllImport(DLL)]
	public static extern ulong FosterSoundGetLengthPcmFrames(IntPtr sound);
	[DllImport(DLL)]
	public static extern ulong FosterSoundGetCursorPcmFrames(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetCursorPcmFrames(IntPtr sound, ulong value);
	[DllImport(DLL, EntryPoint = "FosterSoundGetLooping")]
	private static extern byte FosterSoundGetLoopingNative(IntPtr sound);
	public static FosterBool FosterSoundGetLooping(IntPtr sound) => new(FosterSoundGetLoopingNative(sound));
	[DllImport(DLL, EntryPoint = "FosterSoundSetLooping")]
	private static extern void FosterSoundSetLoopingNative(IntPtr sound, byte value);
	public static void FosterSoundSetLooping(IntPtr sound, FosterBool value) => FosterSoundSetLoopingNative(sound, value ? (byte)1 : (byte)0);
	[DllImport(DLL)]
	public static extern ulong FosterSoundGetLoopBeginPcmFrames(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetLoopBeginPcmFrames(IntPtr sound, ulong value);
	[DllImport(DLL)]
	public static extern ulong FosterSoundGetLoopEndPcmFrames(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetLoopEndPcmFrames(IntPtr sound, ulong value);
	[DllImport(DLL, EntryPoint = "FosterSoundGetSpatialized")]
	private static extern byte FosterSoundGetSpatializedNative(IntPtr sound);
	public static FosterBool FosterSoundGetSpatialized(IntPtr sound) => new(FosterSoundGetSpatializedNative(sound));
	[DllImport(DLL, EntryPoint = "FosterSoundSetSpatialized")]
	private static extern void FosterSoundSetSpatializedNative(IntPtr sound, byte value);
	public static void FosterSoundSetSpatialized(IntPtr sound, FosterBool value) => FosterSoundSetSpatializedNative(sound, value ? (byte)1 : (byte)0);
	[DllImport(DLL, EntryPoint = "Web_FosterSoundGetPosition")]
	private static extern void FosterSoundGetPositionNative(IntPtr sound, out float x, out float y, out float z);
	public static Vector3 FosterSoundGetPosition(IntPtr sound) { FosterSoundGetPositionNative(sound, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterSoundSetPosition")]
	private static extern void FosterSoundSetPositionNative(IntPtr sound, float x, float y, float z);
	public static void FosterSoundSetPosition(IntPtr sound, Vector3 value) => FosterSoundSetPositionNative(sound, value.X, value.Y, value.Z);
	[DllImport(DLL, EntryPoint = "Web_FosterSoundGetVelocity")]
	private static extern void FosterSoundGetVelocityNative(IntPtr sound, out float x, out float y, out float z);
	public static Vector3 FosterSoundGetVelocity(IntPtr sound) { FosterSoundGetVelocityNative(sound, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterSoundSetVelocity")]
	private static extern void FosterSoundSetVelocityNative(IntPtr sound, float x, float y, float z);
	public static void FosterSoundSetVelocity(IntPtr sound, Vector3 value) => FosterSoundSetVelocityNative(sound, value.X, value.Y, value.Z);
	[DllImport(DLL, EntryPoint = "Web_FosterSoundGetDirection")]
	private static extern void FosterSoundGetDirectionNative(IntPtr sound, out float x, out float y, out float z);
	public static Vector3 FosterSoundGetDirection(IntPtr sound) { FosterSoundGetDirectionNative(sound, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterSoundSetDirection")]
	private static extern void FosterSoundSetDirectionNative(IntPtr sound, float x, float y, float z);
	public static void FosterSoundSetDirection(IntPtr sound, Vector3 value) => FosterSoundSetDirectionNative(sound, value.X, value.Y, value.Z);
	[DllImport(DLL)]
	public static extern SoundPositioning FosterSoundGetPositioning(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPositioning(IntPtr sound, SoundPositioning value);
	[DllImport(DLL)]
	public static extern int FosterSoundGetPinnedListenerIndex(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPinnedListenerIndex(IntPtr sound, int value);
	[DllImport(DLL)]
	public static extern SoundAttenuationModel FosterSoundGetAttenuationModel(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetAttenuationModel(IntPtr sound, SoundAttenuationModel value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetRolloff(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetRolloff(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetMinGain(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetMinGain(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetMaxGain(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetMaxGain(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetMinDistance(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetMinDistance(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetMaxDistance(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetMaxDistance(IntPtr sound, float value);
	[DllImport(DLL, EntryPoint = "Web_FosterSoundGetCone")]
	private static extern void FosterSoundGetConeNative(IntPtr sound, out float x, out float y, out float z);
	public static SoundCone FosterSoundGetCone(IntPtr sound) { FosterSoundGetConeNative(sound, out var x, out var y, out var z); return new(x, y, z); }
	[DllImport(DLL, EntryPoint = "Web_FosterSoundSetCone")]
	private static extern void FosterSoundSetConeNative(IntPtr sound, float x, float y, float z);
	public static void FosterSoundSetCone(IntPtr sound, SoundCone value) => FosterSoundSetConeNative(sound, value.InnerAngleInRadians, value.OuterAngleInRadians, value.OuterGain);
	[DllImport(DLL)]
	public static extern float FosterSoundGetDirectionalAttenuationFactor(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetDirectionalAttenuationFactor(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetDopplerFactor(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetDopplerFactor(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern IntPtr FosterSoundGroupCreate(IntPtr parent);
	[DllImport(DLL)]
	public static extern void FosterSoundGroupDestroy(IntPtr soundGroup);
	[DllImport(DLL)]
	public static extern float FosterSoundGroupGetVolume(IntPtr soundGroup);
	[DllImport(DLL)]
	public static extern void FosterSoundGroupSetVolume(IntPtr soundGroup, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGroupGetPitch(IntPtr soundGroup);
	[DllImport(DLL)]
	public static extern void FosterSoundGroupSetPitch(IntPtr soundGroup, float value);
}
