module Sayuri.Capture
open System
open System.Runtime.InteropServices
open Microsoft.Win32.SafeHandles

#nowarn "9"

[<ComImport; Guid "00000001-0000-0000-C000-000000000046"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type IClassFactory =
    abstract member CreateInstance : [<MarshalAs(UnmanagedType.IUnknown)>] pUnkOuter : obj * [<MarshalAs(UnmanagedType.LPStruct)>] riid : Guid * [<Out; MarshalAs(UnmanagedType.Interface)>] ppvObject : obj byref -> unit
    abstract member LockServer : [<MarshalAs(UnmanagedType.Bool)>] fLock : bool -> unit

type D3DFORMAT =
    | UNKNOWN                =  0
    | R8G8B8                 = 20
    | A8R8G8B8               = 21
    | X8R8G8B8               = 22
    | R5G6B5                 = 23
    | X1R5G5B5               = 24
    | A1R5G5B5               = 25
    | A4R4G4B4               = 26
    | R3G3B2                 = 27
    | A8                     = 28
    | A8R3G3B2               = 29
    | X4R4G4B4               = 30
    | A2B10G10R10            = 31
    | A8B8G8R8               = 32
    | X8B8G8R8               = 33
    | G16R16                 = 34
    | A2R10G10B10            = 35
    | A16B16G16R16           = 36
    | A8P8                   = 40
    | P8                     = 41
    | L8                     = 50
    | A8L8                   = 51
    | A4L4                   = 52
    | V8U8                   = 60
    | L6V5U5                 = 61
    | X8L8V8U8               = 62
    | Q8W8V8U8               = 63
    | V16U16                 = 64
    | A2W10V10U10            = 67
    | UYVY                   = 0x59565955
    // | R8G8_B8G8           = MAKEFOURCC('R', 'G', 'B', 'G'),
    | YUY2                   = 0x32595559
    // | G8R8_G8B8           = MAKEFOURCC('G', 'R', 'G', 'B'),
    | DXT1                   = 0x31545844
    | DXT2                   = 0x32545844
    | DXT3                   = 0x33545844
    | DXT4                   = 0x34545844
    | DXT5                   = 0x35545844
    | D16_LOCKABLE           = 70
    | D32                    = 71
    | D15S1                  = 73
    | D24S8                  = 75
    | D24X8                  = 77
    | D24X4S4                = 79
    | D16                    = 80
    | D32F_LOCKABLE          = 82
    | D24FS8                 = 83
    // | D32_LOCKABLE        = 84
    // | S8_LOCKABLE         = 85
    | L16                    = 81
    | VERTEXDATA             = 100
    | INDEX16                = 101
    | INDEX32                = 102
    | Q16W16V16U16           = 110
    // | MULTI2_ARGB8        = MAKEFOURCC('M','E','T','1'),
    | R16F                   = 111
    | G16R16F                = 112
    | A16B16G16R16F          = 113
    | R32F                   = 114
    | G32R32F                = 115
    | A32B32G32R32F          = 116
    | CxV8U8                 = 117
    // | A1                  = 118
    // | A2B10G10R10_XR_BIAS = 119
    // | BINARYBUFFER        = 199

let IID_IDirect3DSurface9 = Guid "0CFBAF3A-9FF6-429a-99B3-A2796AF8B89B"

[<Struct; StructLayout(LayoutKind.Sequential, Pack = 4)>]
type PROPERTYKEY =
    val fmtid : Guid
    val pid : uint32
    new (fmtid, pid) = { fmtid = fmtid; pid = pid }

type VARTYPE = uint16

let private VT_I4   =  3us
let private VT_BOOL = 11us
let private VT_I1   = 16us

[<Struct; StructLayout(LayoutKind.Explicit)>]
type PropVariantUnion =
    [<FieldOffset 0>]
    val mutable boolVal : int16
    [<FieldOffset 0>]
    val mutable lVal : int

[<Struct; StructLayout(LayoutKind.Sequential, Pack = 0)>]
type PROPVARIANT =
    val vt : VARTYPE
    val wReserved1 : uint16
    val wReserved2 : uint16
    val wReserved3 : uint16
    val union : PropVariantUnion
    new (boolVal) = { vt = VT_BOOL; wReserved1 = 0us; wReserved2 = 0us; wReserved3 = 0us; union = PropVariantUnion(boolVal = if boolVal then -1s else 0s); }
    new (lVal)    = { vt = VT_I4;   wReserved1 = 0us; wReserved2 = 0us; wReserved3 = 0us; union = PropVariantUnion(lVal    = lVal);                        }

[<ComImport; Guid "886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type IPropertyStore =
    abstract member GetCount : [<Out>] cProps : uint32 byref -> unit
    abstract member GetAt : iProp : uint32 * [<Out>] pkey : PROPERTYKEY byref -> unit
    abstract member GetValue : [<In>] key : PROPERTYKEY byref * [<Out>] pv : PROPVARIANT byref -> unit
    abstract member SetValue : [<In>] key : PROPERTYKEY byref * [<In>] pv : PROPVARIANT byref -> unit
    abstract member Commit : unit -> unit

#if UNUSED
[<ComImport; Guid "BCDE0395-E52F-467C-8E3D-C4579291692E">]
type MMDeviceEnumerator () =
    class
    end
#endif

type private EDataFlow =
| eRender = 0
| eCapture = 1
| eAll = 2

type private ERole =
| eConsole = 0
| eMultimedia = 1
| eCommunications = 2

[<ComImport; Guid "D666063F-1587-4E43-81F1-B948E807363F"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type private IMMDevice =
    abstract member Activate : [<MarshalAs(UnmanagedType.LPStruct)>] iid : (*REFIID*)Guid * dwClsCtx : uint32 * pActivationParams : PROPVARIANT nativeptr * [<Out; MarshalAs(UnmanagedType.IUnknown)>] ppInterface : obj byref -> unit
    abstract member OpenPropertyStore : stgmAccess : uint32 * [<Out; MarshalAs(UnmanagedType.Interface)>] ppProperties : (*IPropertyStore*)obj byref -> unit
    abstract member GetId : [<Out; MarshalAs(UnmanagedType.LPWStr)>] ppstrId : string byref -> unit
    abstract member GetState : [<Out>] pdwState : uint32 byref -> unit

[<ComImport; Guid "A95664D2-9614-4F35-A746-DE8DB63617E6"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type private IMMDeviceEnumerator =
    abstract member EnumAudioEndpoints : dataFlow : EDataFlow * dwStateMask : uint32 * [<Out; MarshalAs(UnmanagedType.Interface)>] ppDevices : (*IMMDeviceCollection*)obj byref -> unit
    abstract member GetDefaultAudioEndpoint : dataFlow : EDataFlow * role : ERole * [<Out>] ppEndpoint : IMMDevice byref -> unit
    abstract member GetDevice : [<MarshalAs(UnmanagedType.LPWStr)>] pwstrId : string * [<Out>] ppDevice : IMMDevice byref -> unit
    abstract member RegisterEndpointNotificationCallback : [<MarshalAs(UnmanagedType.Interface)>] pClient : (*IMMNotificationClient*)obj -> unit
    abstract member UnregisterEndpointNotificationCallback : [<MarshalAs(UnmanagedType.Interface)>] pClient : (*IMMNotificationClient*)obj -> unit

type private REFERENCE_TIME =
    int64

[<Struct; StructLayout(LayoutKind.Sequential, Pack = 1)>]
type WAVEFORMATEX =
    val wFormatTag : uint16
    val nChannels : uint16
    val nSamplesPerSec : uint32
    val nAvgBytesPerSec : uint32
    val nBlockAlign : uint16
    val wBitsPerSample : uint16
    val cbSize : uint16

[<Literal>]
let private AUDCLNT_STREAMFLAGS_LOOPBACK      = 0x00020000u
[<Literal>]
let private AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000u
[<Literal>]
let private AUDCLNT_STREAMFLAGS_NOPERSIST = 0x00080000u

[<ComImport; Guid "1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type private IAudioClient =
    abstract member Initialize : ShareMode : (*AUDCLNT_SHAREMODE*)int * StreamFlags : uint32 * hnsBufferDuration : REFERENCE_TIME * hnsPeriodicity : REFERENCE_TIME * pFormat : WAVEFORMATEX nativeptr * [<In>] AudioSessionGuid : Guid nativeptr -> unit
    abstract member GetBufferSize : [<Out>] pNumBufferFrames : uint32 byref -> unit
    abstract member GetStreamLatency : [<Out>] phnsLatency : REFERENCE_TIME byref -> unit
    abstract member GetCurrentPadding : [<Out>] pNumPaddingFrames : uint32 byref -> unit
    abstract member IsFormatSupported : ShareMode : (*AUDCLNT_SHAREMODE*)int * pFormat : WAVEFORMATEX * [<Out>] ppClosestMatch : WAVEFORMATEX byref -> unit
    abstract member GetMixFormat : [<Out>] ppDeviceFormat : WAVEFORMATEX nativeptr byref -> unit
    abstract member GetDevicePeriod : [<Out>] phnsDefaultDevicePeriod : REFERENCE_TIME byref * [<Out>] phnsMinimumDevicePeriod : REFERENCE_TIME byref -> unit
    abstract member Start : unit -> unit
    abstract member Stop : unit -> unit
    abstract member Reset : unit -> unit
    abstract member SetEventHandle : eventHandle : SafeWaitHandle -> unit
    abstract member GetService : [<MarshalAs(UnmanagedType.LPStruct)>] riid : (*REFIID*)Guid * [<Out; MarshalAs(UnmanagedType.IUnknown)>] ppv : obj byref -> unit

[<ComImport; Guid "C8ADBD64-E71E-48a0-A4DE-185C395CD317"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type private IAudioCaptureClient =
    [<PreserveSig>]
    abstract member GetBuffer : [<Out>] ppData : nativeint byref * [<Out>] pNumFramesToRead : uint32 byref * [<Out>] pdwFlags : uint32 byref * [<Out>] pu64DevicePosition : uint64 byref * [<Out>] pu64QPCPosition : uint64 byref -> uint32
    abstract member ReleaseBuffer : NumFramesRead : uint32 -> unit
    abstract member GetNextPacketSize : [<Out>] pNumFramesInNextPacket : uint32 byref -> unit

[<ComImport; Guid "5BC8A76B-869A-46a3-9B03-FA218A66AEBE"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); AllowNullLiteral>]
type IMFCollection =
    abstract member GetElementCount : [<Out>] pcElements : uint32 byref -> unit
    abstract member GetElement : dwElementIndex : uint32 * [<Out; MarshalAs(UnmanagedType.IUnknown)>] ppUnkElement : obj byref -> unit
    abstract member AddElement : [<MarshalAs(UnmanagedType.IUnknown)>] pUnkElement : obj -> unit
    abstract member RemoveElement : dwElementIndex : uint32 * [<Out; MarshalAs(UnmanagedType.IUnknown)>] ppUnkElement : obj byref -> unit
    abstract member InsertElementAt : dwIndex : uint32 * [<MarshalAs(UnmanagedType.IUnknown)>] pUnknown : obj -> unit
    abstract member RemoveAllElements : unit -> unit

#if UNUSED
[<ComImport; Guid "87CE5498-68D6-44E5-9215-6DA47EF883D8"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type private ISimpleAudioVolume =
    abstract member SetMasterVolume : fLevel : single * [<In>] EventContext : Guid nativeptr -> unit
    abstract member GetMasterVolume : [<Out>] pfLevel : single byref -> unit
    abstract member SetMute : bMute : bool * [<In>] EventContext : Guid nativeptr -> unit
    abstract member GetMute : [<Out>] pbMute : bool byref -> unit
#endif

// MF_VERSION has following 2 values.
//   0x00010070:  success on Win7, Vista, WinXP with Windows Media Format 11 SDK,  failed on WinXP.
//   0x00020070:  success on Win7,        WinXP with Windows Media Format 11 SDK.  failed on WinXP, Vista, Vista with Platform Update Supplement.
// So, version testing is no meaning.
[<Literal>]
let private MF_VERSION = 0x00010070
[<Literal>]
let private MFSTARTUP_NOSOCKET = 0x1

[<Struct>]
type MF_SINK_WRITER_STATISTICS =
    val mutable cb : uint32
    val llLastTimestampReceived : int64
    val llLastTimestampEncoded : int64
    val llLastTimestampProcessed : int64
    val llLastStreamTickReceived : int64
    val llLastSinkSampleRequest : int64
    val qwNumSamplesReceived : uint64
    val qwNumSamplesEncoded : uint64
    val qwNumSamplesProcessed : uint64
    val qwNumStreamTicksReceived : uint64
    val dwByteCountQueued : uint32
    val qwByteCountProcessed : uint64
    val dwNumOutstandingSinkSampleRequests : uint32
    val dwAverageSampleRateReceived : uint32
    val dwAverageSampleRateEncoded : uint32
    val dwAverageSampleRateProcessed : uint32

[<ComImport; Guid "2cd2d921-c447-44a7-a13c-4adabfc247e3"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); AllowNullLiteral>]
type IMFAttributes =
    abstract member GetItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pValue : (*PROPVARIANT*)obj byref -> unit
    abstract member GetItemType : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pType : (*MF_ATTRIBUTE_TYPE*)obj byref -> unit
    abstract member CompareItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * Value : (*REFPROPVARIANT*)obj * [<Out>] pbResult : bool byref -> unit
    abstract member Compare : pTheirs : IMFAttributes * MatchType : (*MF_ATTRIBUTES_MATCH_TYPE*)obj * [<Out>] pbResult : bool byref -> unit
    abstract member GetUINT32 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] punValue : uint32 byref -> unit
    abstract member GetUINT64 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] punValue : uint64 byref -> unit
    abstract member GetDouble : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pfValue : float byref -> unit
    abstract member GetGUID : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pguidValue : Guid byref -> unit
    abstract member GetStringLength : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pcchLength : uint32 byref -> unit
    abstract member GetString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pwszValue : string byref * cchBufSize : uint32 * pcchLength : uint32 byref -> unit
    abstract member GetAllocatedString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] ppwszValue : string byref * [<Out>] pcchLength : uint32 byref -> unit
    abstract member GetBlobSize : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pcbBlobSize : uint32 byref -> unit
    abstract member GetBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pBuf : (*UINT8*)nativeint * cbBufSize : uint32 * [<Out>] pcbBlobSize : uint32 byref -> unit
    abstract member GetAllocatedBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] ppBuf : (*UINT8*)nativeint byref * [<Out>] pcbSize : uint32 byref -> unit
    abstract member GetUnknown : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] riid : Guid * [<Out>] ppv : obj byref -> unit
    abstract member SetItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * Value : (*REFPROPVARIANT*)obj -> unit
    abstract member DeleteItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid -> unit
    abstract member DeleteAllItems : unit -> unit
    abstract member SetUINT32 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * unValue : uint32 -> unit
    abstract member SetUINT64 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * unValue : uint64 -> unit
    abstract member SetDouble : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * fValue : float -> unit
    abstract member SetGUID : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] guidValue : Guid -> unit
    abstract member SetString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * wszValue : string -> unit
    abstract member SetBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pBuf : (*UINT8*)nativeint byref * cbBufSize : uint32 -> unit
    abstract member SetUnknown : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.IUnknown)>] pUnknown : obj -> unit
    abstract member LockStore : unit -> unit
    abstract member UnlockStore : unit -> unit
    abstract member GetCount : [<Out>] pcItems : uint32 byref -> unit
    abstract member GetItemByIndex : unIndex : uint32 * [<Out>] pguidKey : Guid byref * pValue : (*PROPVARIANT*)obj byref -> unit
    abstract member CopyAllItems : pDest : IMFAttributes -> unit

[<ComImport; Guid "44ae0fa8-ea31-4109-8d2e-4cae4997c555"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); AllowNullLiteral>]
type IMFMediaType =
    //inherit IMFAttributes
    abstract member GetItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pValue : (*PROPVARIANT*)obj byref -> unit
    abstract member GetItemType : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pType : (*MF_ATTRIBUTE_TYPE*)obj byref -> unit
    abstract member CompareItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * Value : (*REFPROPVARIANT*)obj * [<Out>] pbResult : bool byref -> unit
    abstract member Compare : pTheirs : IMFAttributes * MatchType : (*MF_ATTRIBUTES_MATCH_TYPE*)obj * [<Out>] pbResult : bool byref -> unit
    abstract member GetUINT32 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] punValue : uint32 byref -> unit
    abstract member GetUINT64 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] punValue : uint64 byref -> unit
    abstract member GetDouble : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pfValue : float byref -> unit
    abstract member GetGUID : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pguidValue : Guid byref -> unit
    abstract member GetStringLength : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pcchLength : uint32 byref -> unit
    abstract member GetString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pwszValue : string byref * cchBufSize : uint32 * pcchLength : uint32 byref -> unit
    abstract member GetAllocatedString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] ppwszValue : string byref * [<Out>] pcchLength : uint32 byref -> unit
    abstract member GetBlobSize : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pcbBlobSize : uint32 byref -> unit
    abstract member GetBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pBuf : (*UINT8*)nativeint * cbBufSize : uint32 * [<Out>] pcbBlobSize : uint32 byref -> unit
    abstract member GetAllocatedBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] ppBuf : (*UINT8*)nativeint byref * [<Out>] pcbSize : uint32 byref -> unit
    abstract member GetUnknown : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] riid : Guid * [<Out>] ppv : obj byref -> unit
    abstract member SetItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * Value : (*REFPROPVARIANT*)obj -> unit
    abstract member DeleteItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid -> unit
    abstract member DeleteAllItems : unit -> unit
    abstract member SetUINT32 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * unValue : uint32 -> unit
    abstract member SetUINT64 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * unValue : uint64 -> unit
    abstract member SetDouble : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * fValue : float -> unit
    abstract member SetGUID : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] guidValue : Guid -> unit
    abstract member SetString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * wszValue : string -> unit
    abstract member SetBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pBuf : (*UINT8*)nativeint byref * cbBufSize : uint32 -> unit
    abstract member SetUnknown : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.IUnknown)>] pUnknown : obj -> unit
    abstract member LockStore : unit -> unit
    abstract member UnlockStore : unit -> unit
    abstract member GetCount : [<Out>] pcItems : uint32 byref -> unit
    abstract member GetItemByIndex : unIndex : uint32 * [<Out>] pguidKey : Guid byref * pValue : (*PROPVARIANT*)obj byref -> unit
    abstract member CopyAllItems : pDest : IMFAttributes -> unit

    abstract member GetMajorType : [<Out>] pguidMajorType : Guid byref -> unit
    abstract member IsCompressedFormat : [<Out>] pfCompressed : bool byref -> unit
    abstract member IsEqual : pIMediaType : IMFMediaType * [<Out>] pdwFlags : uint32 byref -> unit
    abstract member GetRepresentation : guidRepresentation : Guid * [<Out>] ppvRepresentation : nativeint byref -> unit
    abstract member FreeRepresentation : guidRepresentation : Guid * pvRepresentation : obj -> unit

[<ComImport; Guid "045FA593-8799-42b8-BC8D-8968C6453507"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); AllowNullLiteral>]
type IMFMediaBuffer =
    abstract member Lock : [<Out>] ppbBuffer : nativeint byref * [<Out>] pcbMaxLength : uint32 byref * [<Out>] pcbCurrentLength : uint32 byref -> unit
    abstract member Unlock : unit -> unit
    abstract member GetCurrentLength : [<Out>] pcbCurrentLength : uint32 byref -> unit
    abstract member SetCurrentLength : cbCurrentLength : uint32 -> unit
    abstract member GetMaxLength : [<Out>] pcbMaxLength : uint32 byref -> unit

[<ComImport; Guid "c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); AllowNullLiteral>]
type IMFSample =
    //inherit IMFAttributes
    abstract member GetItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pValue : (*PROPVARIANT*)obj byref -> unit
    abstract member GetItemType : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pType : (*MF_ATTRIBUTE_TYPE*)obj byref -> unit
    abstract member CompareItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * Value : (*REFPROPVARIANT*)obj * [<Out>] pbResult : bool byref -> unit
    abstract member Compare : pTheirs : IMFAttributes * MatchType : (*MF_ATTRIBUTES_MATCH_TYPE*)obj * [<Out>] pbResult : bool byref -> unit
    abstract member GetUINT32 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] punValue : uint32 byref -> unit
    abstract member GetUINT64 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] punValue : uint64 byref -> unit
    abstract member GetDouble : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pfValue : float byref -> unit
    abstract member GetGUID : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pguidValue : Guid byref -> unit
    abstract member GetStringLength : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pcchLength : uint32 byref -> unit
    abstract member GetString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pwszValue : string byref * cchBufSize : uint32 * pcchLength : uint32 byref -> unit
    abstract member GetAllocatedString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] ppwszValue : string byref * [<Out>] pcchLength : uint32 byref -> unit
    abstract member GetBlobSize : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pcbBlobSize : uint32 byref -> unit
    abstract member GetBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pBuf : (*UINT8*)nativeint * cbBufSize : uint32 * [<Out>] pcbBlobSize : uint32 byref -> unit
    abstract member GetAllocatedBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] ppBuf : (*UINT8*)nativeint byref * [<Out>] pcbSize : uint32 byref -> unit
    abstract member GetUnknown : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] riid : Guid * [<Out>] ppv : obj byref -> unit
    abstract member SetItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * Value : (*REFPROPVARIANT*)obj -> unit
    abstract member DeleteItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid -> unit
    abstract member DeleteAllItems : unit -> unit
    abstract member SetUINT32 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * unValue : uint32 -> unit
    abstract member SetUINT64 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * unValue : uint64 -> unit
    abstract member SetDouble : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * fValue : float -> unit
    abstract member SetGUID : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] guidValue : Guid -> unit
    abstract member SetString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * wszValue : string -> unit
    abstract member SetBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pBuf : (*UINT8*)nativeint byref * cbBufSize : uint32 -> unit
    abstract member SetUnknown : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.IUnknown)>] pUnknown : obj -> unit
    abstract member LockStore : unit -> unit
    abstract member UnlockStore : unit -> unit
    abstract member GetCount : [<Out>] pcItems : uint32 byref -> unit
    abstract member GetItemByIndex : unIndex : uint32 * [<Out>] pguidKey : Guid byref * pValue : (*PROPVARIANT*)obj byref -> unit
    abstract member CopyAllItems : pDest : IMFAttributes -> unit

    abstract member GetSampleFlags : [<Out>] pdwSampleFlags : uint32 byref -> unit
    abstract member SetSampleFlags : dwSampleFlags : uint32 -> unit
    abstract member GetSampleTime : [<Out>] phnsSampleTime : int64 byref -> unit
    abstract member SetSampleTime : hnsSampleTime : int64 -> unit
    abstract member GetSampleDuration : [<Out>] phnsSampleDuration : int64 byref -> unit
    abstract member SetSampleDuration : hnsSampleDuration : int64 -> unit
    abstract member GetBufferCount : [<Out>] pdwBufferCount : uint32 byref -> unit
    abstract member GetBufferByIndex : dwIndex : uint32 * [<Out>] ppBuffer : IMFMediaBuffer byref -> unit
    abstract member ConvertToContiguousBuffer : [<Out>] ppBuffer : IMFMediaBuffer byref -> unit
    abstract member AddBuffer : pBuffer : IMFMediaBuffer -> unit
    abstract member RemoveBufferByIndex : dwIndex : uint32 -> unit
    abstract member RemoveAllBuffers : unit -> unit
    abstract member GetTotalLength : [<Out>] pcbTotalLength : uint32 byref -> unit
    abstract member CopyToBuffer : pBuffer : IMFMediaBuffer -> unit

[<ComImport; Guid "7FEE9E9A-4A89-47a6-899C-B6A53A70FB67"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); AllowNullLiteral>]
type IMFActivate =
    //inherit IMFAttributes
    abstract member GetItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pValue : (*PROPVARIANT*)obj byref -> unit
    abstract member GetItemType : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pType : (*MF_ATTRIBUTE_TYPE*)obj byref -> unit
    abstract member CompareItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * Value : (*REFPROPVARIANT*)obj * [<Out>] pbResult : bool byref -> unit
    abstract member Compare : pTheirs : IMFAttributes * MatchType : (*MF_ATTRIBUTES_MATCH_TYPE*)obj * [<Out>] pbResult : bool byref -> unit
    abstract member GetUINT32 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] punValue : uint32 byref -> unit
    abstract member GetUINT64 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] punValue : uint64 byref -> unit
    abstract member GetDouble : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pfValue : float byref -> unit
    abstract member GetGUID : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pguidValue : Guid byref -> unit
    abstract member GetStringLength : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pcchLength : uint32 byref -> unit
    abstract member GetString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pwszValue : string byref * cchBufSize : uint32 * pcchLength : uint32 byref -> unit
    abstract member GetAllocatedString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] ppwszValue : nativeint byref * [<Out>] pcchLength : uint32 byref -> unit
    abstract member GetBlobSize : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] pcbBlobSize : uint32 byref -> unit
    abstract member GetBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pBuf : (*UINT8*)nativeint * cbBufSize : uint32 * [<Out>] pcbBlobSize : uint32 byref -> unit
    abstract member GetAllocatedBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<Out>] ppBuf : (*UINT8*)nativeint byref * [<Out>] pcbSize : uint32 byref -> unit
    abstract member GetUnknown : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] riid : Guid * [<Out>] ppv : obj byref -> unit
    abstract member SetItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * Value : (*REFPROPVARIANT*)obj -> unit
    abstract member DeleteItem : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid -> unit
    abstract member DeleteAllItems : unit -> unit
    abstract member SetUINT32 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * unValue : uint32 -> unit
    abstract member SetUINT64 : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * unValue : uint64 -> unit
    abstract member SetDouble : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * fValue : float -> unit
    abstract member SetGUID : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] guidValue : Guid -> unit
    abstract member SetString : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * wszValue : string -> unit
    abstract member SetBlob : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * pBuf : (*UINT8*)nativeint byref * cbBufSize : uint32 -> unit
    abstract member SetUnknown : [<MarshalAs(UnmanagedType.LPStruct)>] guidKey : Guid * [<MarshalAs(UnmanagedType.IUnknown)>] pUnknown : obj -> unit
    abstract member LockStore : unit -> unit
    abstract member UnlockStore : unit -> unit
    abstract member GetCount : [<Out>] pcItems : uint32 byref -> unit
    abstract member GetItemByIndex : unIndex : uint32 * [<Out>] pguidKey : Guid byref * pValue : (*PROPVARIANT*)obj byref -> unit
    abstract member CopyAllItems : pDest : IMFAttributes -> unit

    abstract member ActivateObject : [<MarshalAs(UnmanagedType.LPStruct)>] riid : Guid -> [<MarshalAs(UnmanagedType.Interface)>] obj
    abstract member ShutdownObject : unit -> unit
    abstract member DetachObject : unit -> unit

[<ComImport; Guid "bf94c121-5b05-4e6f-8000-ba598961414d"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type IMFTransform =
    abstract member GetStreamLimits : [<Out>] pdwInputMinimum : uint32 byref * [<Out>] pdwInputMaximum : uint32 byref * [<Out>] pdwOutputMinimum : uint32 byref * [<Out>] pdwOutputMaximum : uint32 byref -> unit
    abstract member GetStreamCount : [<Out>] pcInputStreams : uint32 byref * [<Out>] pcOutputStreams : uint32 byref -> unit
    abstract member GetStreamIDs : dwInputIDArraySize : uint32 * [<Out>] pdwInputIDs : uint32 byref * dwOutputIDArraySize : uint32 * [<Out>] pdwOutputIDs : uint32 byref -> unit
    abstract member GetInputStreamInfo : dwInputStreamID : uint32 * [<Out>] pStreamInfo : (*MFT_INPUT_STREAM_INFO*)nativeint byref -> unit
    abstract member GetOutputStreamInfo : dwOutputStreamID : uint32 * [<Out>] pStreamInfo : (*MFT_OUTPUT_STREAM_INFO*)nativeint byref -> unit
    abstract member GetAttributes : [<Out>] pAttributes : IMFAttributes byref -> unit
    abstract member GetInputStreamAttributes : dwInputStreamID : uint32 * [<Out>] pAttributes : IMFAttributes byref -> unit
    abstract member GetOutputStreamAttributes : dwOutputStreamID : uint32 * [<Out>] pAttributes : IMFAttributes byref -> unit
    abstract member DeleteInputStream : dwStreamID : uint32 -> unit
    abstract member AddInputStreams : cStreams : uint32 * [<MarshalAs(UnmanagedType.LPArray)>] adwStreamIDs : uint32[] -> unit
    abstract member GetInputAvailableType : dwInputStreamID : uint32 * dwTypeIndex : uint32 * [<Out>] ppType : IMFMediaType byref -> unit
    abstract member GetOutputAvailableType : dwOutputStreamID : uint32 * dwTypeIndex : uint32 * [<Out>] ppType : IMFMediaType byref -> unit
    abstract member SetInputType : dwInputStreamID : uint32 * pType : IMFMediaType * dwFlags : uint32 -> unit
    abstract member SetOutputType : dwOutputStreamID : uint32 * pType : IMFMediaType * dwFlags : uint32 -> unit
    abstract member GetInputCurrentType : dwInputStreamID : uint32 * [<Out>] ppType : IMFMediaType byref -> unit
    abstract member GetOutputCurrentType : dwOutputStreamID : uint32 * [<Out>] ppType : IMFMediaType byref -> unit
    abstract member GetInputStatus : dwInputStreamID : uint32 * [<Out>] pdwFlags : (*MFT_INPUT_STATUS_ACCEPT_DATA*)uint32 byref -> unit
    abstract member GetOutputStatus : [<Out>] pdwFlags : uint32 byref -> unit
    abstract member SetOutputBounds : hnsLowerBound : int64 * hnsUpperBound : int64 -> unit
    abstract member ProcessEvent : dwInputStreamID : uint32 * [<MarshalAs(UnmanagedType.Interface)>] pEvent : (*IMFMediaEvent*)obj -> unit
    abstract member ProcessMessage : eMessage : (*MFT_MESSAGE_TYPE*)uint32 * ulParam : (*ULONG_PTR*)nativeint -> unit
    abstract member ProcessInput : dwInputStreamID : uint32 * pSample : IMFSample * dwFlags : uint32 -> unit
    abstract member ProcessOutput : dwFlags : (*MFT_PROCESS_OUTPUT_FLAGS*)uint32 * cOutputBufferCount : uint32 * [<In; Out>] cOutputBufferCount : (*MFT_OUTPUT_DATA_BUFFER*)nativeint * [<Out>] pdwStatus : (*MFT_PROCESS_OUTPUT_XXX*)uint32 byref -> unit

[<ComImport; Guid "3137f1cd-fe5e-4805-a5d8-fb477448cb3d"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); AllowNullLiteral>]
type IMFSinkWriter =
    abstract member AddStream : pTargetMediaType : IMFMediaType * [<Out>] pdwStreamIndex : uint32 byref -> unit
    abstract member SetInputMediaType : dwStreamIndex : uint32 * pInputMediaType : IMFMediaType * pEncodingParameters : IMFAttributes -> unit
    abstract member BeginWriting : unit -> unit
    abstract member WriteSample : dwStreamIndex : uint32 * pSample : IMFSample -> unit
    abstract member SendStreamTick : dwStreamIndex : uint32 * llTimestamp : int64 -> unit
    abstract member PlaceMarker : dwStreamIndex : uint32 * pvContext : obj -> unit
    abstract member NotifyEndOfSegment : dwStreamIndex : uint32 -> unit
    abstract member Flush : dwStreamIndex : uint32 -> unit
    abstract member Finalize : unit -> unit
    abstract member GetServiceForStream : dwStreamIndex : uint32 * [<MarshalAs(UnmanagedType.LPStruct)>] guidService : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] riid : Guid * [<Out; MarshalAs(UnmanagedType.IUnknown)>] ppvObject : obj byref -> unit
    abstract member GetStatistics : dwStreamIndex : uint32 * pStats : MF_SINK_WRITER_STATISTICS byref -> unit

[<ComImport; Guid "666f76de-33d2-41b9-a458-29ed0a972c58"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type IMFSinkWriterCallback =
    abstract member OnFinalize : hrStatus : int -> unit
    abstract member OnMarker : dwStreamIndex : uint32 * pvContext : nativeint -> unit

[<StructLayout(LayoutKind.Sequential); AllowNullLiteral>]
type MFT_REGISTER_TYPE_INFO =
    val guidMajorType : Guid
    val guidSubtype : Guid
    new (guidMajorType, guidSubtype) = { guidMajorType = guidMajorType; guidSubtype = guidSubtype }

[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFStartup(int Version, int dwFlags);
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFShutdown();
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFCreateAttributes(IMFAttributes& ppMFAttributes, uint32 cInitialSize);
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFCreateMediaType([<Out>] IMFMediaType& ppMFType);
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFCreateAlignedMemoryBuffer(uint32 cbMaxLength, uint32 fAlignmentFlags, [<Out>] IMFMediaBuffer& ppBuffer);
[<DllImport("Evr.dll", PreserveSig = false)>]
extern void private MFCreateDXSurfaceBuffer([<MarshalAs(UnmanagedType.LPStruct)>] Guid riid, [<MarshalAs(UnmanagedType.IUnknown)>] obj punkSurface, bool fBottomUpWhenLinear, [<Out>] IMFMediaBuffer& ppBuffer);
[<DllImport((*"Mfplat.dll"*)"Evr.dll", PreserveSig = false)>]
extern void private MFCopyImage(nativeint pDest, (*int*)uint32 lDestStride, nativeint pSrc, (*int*)uint32 lSrcStride, uint32 dwWidthInBytes, uint32 dwLines);
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFCreateSample([<Out>] IMFSample& ppIMFSample);
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFInitMediaTypeFromWaveFormatEx(IMFMediaType pMFType, WAVEFORMATEX* pWaveFormat, uint32 cbBufSize);
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFTRegisterLocal(IClassFactory pClassFactory, [<MarshalAs(UnmanagedType.LPStruct)>] Guid guidCategory, string pszName, uint32 Flags, uint32 cInputTypes, MFT_REGISTER_TYPE_INFO pInputTypes, uint32 cOutputTypes, MFT_REGISTER_TYPE_INFO pOutputTypes);
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFTUnregisterLocal(IClassFactory pClassFactory);
[<DllImport("Mfplat.dll", PreserveSig = false)>]
extern void private MFTEnumEx(Guid guidCategory, uint32 Flags, MFT_REGISTER_TYPE_INFO pInputType, MFT_REGISTER_TYPE_INFO pOutputType, [<Out>] nativeint& pppMFTActivate, [<Out>] uint32& pcMFTActivate);

[<DllImport("Mf.dll", PreserveSig = false)>]
extern void private MFTranscodeGetAudioOutputAvailableTypes([<MarshalAs(UnmanagedType.LPStruct)>] Guid guidSubType, uint32 dwMFTFlags, IMFAttributes pCodecConfig, [<Out>] IMFCollection& ppAvailableTypes);

[<DllImport("Mfreadwrite.dll", PreserveSig = false, CharSet = CharSet.Unicode)>]
extern void private MFCreateSinkWriterFromURL(string pwszOutputURL, (*IMFByteStream *)nativeint pByteStream, IMFAttributes pAttributes, [<Out>] IMFSinkWriter& ppSinkWriter);

let inline private Pack2UINT32AsUINT64 (unHigh : uint32) (unLow : uint32) =
    uint64 unHigh <<< 32 ||| uint64 unLow

let inline private MFSetAttribute2UINT32asUINT64 (pAttributes : IMFMediaType, guidKey, unHigh32, unLow32) =
    pAttributes.SetUINT64(guidKey, Pack2UINT32AsUINT64 unHigh32 unLow32)

let inline private MFSetAttributeSize (pAttributes, guidKey, unWidth, unHeight) =
    MFSetAttribute2UINT32asUINT64(pAttributes, guidKey, unWidth, unHeight)

let inline private MFSetAttributeRatio (pAttributes, guidKey, unNumerator, unDenominator) =
    MFSetAttribute2UINT32asUINT64(pAttributes, guidKey, unNumerator, unDenominator)

let inline private MFGetAttributeUINT32 (pAttributes : IMFMediaType, guidKey, unDefault) =
    try
        pAttributes.GetUINT32(guidKey)
    with _ -> unDefault

let private MF_MT_MAJOR_TYPE                        = Guid "48EBA18E-F8C9-4687-BF11-0A74C9F96A8F"
let private MF_MT_SUBTYPE                           = Guid "F7E34C9A-42E8-4714-B74B-CB29D72C35E5"
let private MF_MT_AVG_BITRATE                       = Guid "20332624-FB0D-4D9E-BD0D-CBF6786C102E"
let private MF_MT_INTERLACE_MODE                    = Guid "E2724BB8-E676-4806-B4B2-A8D6EFB44CCD"
let private MF_MT_FRAME_SIZE                        = Guid "1652C33D-D6B2-4012-B834-72030849A37D"
let private MF_MT_FRAME_RATE                        = Guid "C459A2E8-3D2C-4E44-B132-FEE5156C7BB0"
let private MF_MT_PIXEL_ASPECT_RATIO                = Guid "C6376A1E-8D0A-4027-BE45-6D9A0AD39BB6"
let private MF_MT_DEFAULT_STRIDE                    = Guid "644b4e48-1e02-4516-b0eb-c01ca9d49ac6"
let private MF_MT_MPEG2_PROFILE                     = Guid "AD76A80B-2D5C-4E0B-B375-64E520137036"
let private MF_MT_MPEG2_LEVEL                       = Guid "96F66574-11C5-4015-8666-BFF516436DA7"
let private MF_MT_AUDIO_NUM_CHANNELS                = Guid "37E48BF5-645E-4C5B-89DE-ADA9E29B696A"
let private MF_MT_AUDIO_SAMPLES_PER_SECOND          = Guid "5FAEEAE7-0290-4C31-9E8A-C534F68D9DBA"
let private MF_MT_AUDIO_AVG_BYTES_PER_SECOND        = Guid "1AAB75C8-CFEF-451C-AB95-AC034B8E1731"
let private MF_MT_AUDIO_BITS_PER_SAMPLE             = Guid "F2DEB57F-40FA-4764-AA33-ED4F2D1FF669"
let private MF_MT_AUDIO_BLOCK_ALIGNMENT             = Guid "322DE230-9EEB-43BD-AB7A-FF412251541D"
let private MF_MT_ORIGINAL_WAVE_FORMAT_TAG          = Guid "8CBBC843-9FD9-49C2-882F-A72586C408AD"
let private MF_MT_SAMPLE_SIZE                       = Guid "DAD3AB78-1990-408B-BCE2-EBA673DACC10"
let private MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = Guid "A634A91C-822B-41B9-A494-4DE4643612B0"
let private MF_SINK_WRITER_DISABLE_THROTTLING       = Guid "08B845D8-2B74-4AFE-9D53-BE16D2D5AE4F"
let private MF_SINK_WRITER_ASYNC_CALLBACK           = Guid "48CB183E-7B0B-46F4-822E-5E1D2DDA4354"
let private MFT_CATEGORY_VIDEO_ENCODER              = Guid "F79EAC7D-E545-4387-BDEE-D647D7BDE42A"
let private MFT_CATEGORY_AUDIO_ENCODER              = Guid "91C64BD0-F91E-4D8C-9276-DB248279D975"
let private MFT_FRIENDLY_NAME_Attribute             = Guid "314FFBAE-5B41-4C95-9C19-4E7D586FACE3"
let private MFT_TRANSFORM_CLSID_Attribute           = Guid "6821C42B-65A4-4E82-99BC-9A88205ECD0C"
let private MFT_OUTPUT_TYPES_Attributes             = Guid "8EAE8CF3-A44F-4306-BA5C-BF5DDA242818"
let private MFMediaType_Video                       = Guid "73646976-0000-0010-8000-00AA00389B71"
let private MFMediaType_Audio                       = Guid "73647561-0000-0010-8000-00AA00389B71"
let private MFVideoFormat_WMV3                      = Guid "33564D57-0000-0010-8000-00AA00389B71"
let private MFVideoFormat_H264                      = Guid "34363248-0000-0010-8000-00AA00389B71"
let private MFVideoFormat_RGB32                     = Guid "00000016-0000-0010-8000-00AA00389B71"
let private MFAudioFormat_WMAudioV9                 = Guid "00000162-0000-0010-8000-00AA00389B71"
let private MFAudioFormat_AAC                       = Guid "00001610-0000-0010-8000-00AA00389B71"
let private AM_MEDIA_TYPE_REPRESENTATION            = Guid "E2E42AD2-132C-491E-A268-3C7C2DCA181F"
let private MFVideoInterlace_Progressive = 2u
let private MF_SINK_WRITER_ALL_STREAMS = 0xFFFFFFFEu

[<Struct; StructLayout(LayoutKind.Sequential, Pack = 0)>]
type AM_MEDIA_TYPE =
    val majortype : Guid
    val subtype : Guid
    val bFixedSizeSamples : (*bool*)int
    val bTemporalCompression : (*bool*)int
    val lSampleSize : uint32
    val formattype : Guid
    val pUnk : (*IUnknown*)nativeint
    val cbFormat : uint32
    val pbFormat : nativeint

let private MFT_ENUM_FLAG_SYNCMFT                       = 0x00000001u
let private MFT_ENUM_FLAG_ASYNCMFT                      = 0x00000002u
let private MFT_ENUM_FLAG_HARDWARE                      = 0x00000004u
let private MFT_ENUM_FLAG_FIELDOFUSE                    = 0x00000008u
let private MFT_ENUM_FLAG_LOCALMFT                      = 0x00000010u
let private MFT_ENUM_FLAG_TRANSCODE_ONLY                = 0x00000020u
let private MFT_ENUM_FLAG_SORTANDFILTER                 = 0x00000040u
let private MFT_ENUM_FLAG_SORTANDFILTER_APPROVED_ONLY   = 0x000000C0u
let private MFT_ENUM_FLAG_SORTANDFILTER_WEB_ONLY        = 0x00000140u
let private MFT_ENUM_FLAG_ALL                           = 0x0000003Fu

let private eAVEncH264VProfile_unknown                    = 0u
let private eAVEncH264VProfile_Simple                     = 66u
let private eAVEncH264VProfile_Base                       = 66u
let private eAVEncH264VProfile_Main                       = 77u
let private eAVEncH264VProfile_High                       = 100u
let private eAVEncH264VProfile_422                        = 122u
let private eAVEncH264VProfile_High10                     = 110u
let private eAVEncH264VProfile_444                        = 144u
let private eAVEncH264VProfile_Extended                   = 88u
let private eAVEncH264VProfile_ScalableBase               = 83u
let private eAVEncH264VProfile_ScalableHigh               = 86u
let private eAVEncH264VProfile_MultiviewHigh              = 118u
let private eAVEncH264VProfile_StereoHigh                 = 128u
let private eAVEncH264VProfile_ConstrainedBase            = 256u
let private eAVEncH264VProfile_UCConstrainedHigh          = 257u
let private eAVEncH264VProfile_UCScalableConstrainedBase  = 258u
let private eAVEncH264VProfile_UCScalableConstrainedHigh  = 259u
let private eAVEncH264VLevel1    = 10u
let private eAVEncH264VLevel1_b  = 11u
let private eAVEncH264VLevel1_1  = 11u
let private eAVEncH264VLevel1_2  = 12u
let private eAVEncH264VLevel1_3  = 13u
let private eAVEncH264VLevel2    = 20u
let private eAVEncH264VLevel2_1  = 21u
let private eAVEncH264VLevel2_2  = 22u
let private eAVEncH264VLevel3    = 30u
let private eAVEncH264VLevel3_1  = 31u
let private eAVEncH264VLevel3_2  = 32u
let private eAVEncH264VLevel4    = 40u
let private eAVEncH264VLevel4_1  = 41u
let private eAVEncH264VLevel4_2  = 42u
let private eAVEncH264VLevel5    = 50u
let private eAVEncH264VLevel5_1  = 51u
let private eAVEncH264VLevel5_2  = 52u

let private MFPKEY_VBRENABLED                  = PROPERTYKEY(Guid "E48D9459-6ABE-4EB5-9211-60080C1AB984", 0x0014u)
let private MFPKEY_NUMTHREADS                  = PROPERTYKEY(Guid "4E91BF89-665A-49DA-BB94-88C550CFCD28", 0x003Au)
let private MFPKEY_COMPRESSIONOPTIMIZATIONTYPE = PROPERTYKEY(Guid "4E91BF89-665A-49DA-BB94-88C550CFCD28", 0x0043u)

let private VIDEO_BIT_RATE = 8000000u

open System.Diagnostics
open System.Drawing
open System.Drawing.Imaging
open System.IO
open System.Text
open System.Threading
open System.Windows.Forms
open Microsoft.FSharp.NativeInterop
open Sayuri.FSharp.Local

[<DllImport("Kernel32.dll", CharSet = CharSet.Unicode)>]
extern nativeint private LoadLibrary(string lpFileName);
type Callback = delegate of qpc : int64 * [<MarshalAs(UnmanagedType.IUnknown)>] surface : obj -> unit
[<DllImport("d3d9.dll")>]
extern void private GetParameter([<Out>] uint32& width, [<Out>] uint32& height, [<Out>] D3DFORMAT& format, [<Out>] uint32& fps);
[<DllImport("d3d9.dll")>]
extern void private Start(Callback callback);
[<DllImport("d3d9.dll")>]
extern void private Stop();

let table = dict [ "wmv", (MFVideoFormat_WMV3, MFAudioFormat_WMAudioV9); "mp4", (MFVideoFormat_H264, MFAudioFormat_AAC) ]

let private enumMFT category outputType =
    let mutable ppMFTActivate = 0n
    let mutable cMFTActivate = 0u
    try
        MFTEnumEx(category, MFT_ENUM_FLAG_SYNCMFT ||| MFT_ENUM_FLAG_ASYNCMFT ||| MFT_ENUM_FLAG_HARDWARE ||| MFT_ENUM_FLAG_SORTANDFILTER, null, outputType, &ppMFTActivate, &cMFTActivate)
        let ppMFTActivate = NativePtr.ofNativeInt ppMFTActivate
        Array.init (int cMFTActivate) (fun i -> NativePtr.get ppMFTActivate i |> Marshal.GetObjectForIUnknown :?> IMFActivate)
    finally
        Marshal.FreeCoTaskMem ppMFTActivate

type MFTransformClassFactory (majorType, subType, preferredCodec) as this =
    // see http://social.msdn.microsoft.com/Forums/en-us/6da521e9-7bb3-4b79-a2b6-b31509224638#5a7d3b0e-0505-4375-9969-66ef5021ec25
    let iuknown = Guid "00000000-0000-0000-c000-000000000046"
    let outputType = MFT_REGISTER_TYPE_INFO(majorType, subType)
    do
        MFTRegisterLocal(this, MFT_CATEGORY_VIDEO_ENCODER, "KanHQ Class Factory", 0u, 0u, null, 1u, outputType)
    interface IClassFactory with
        member this.CreateInstance(pUnkOuter, riid, ppvObject) =
            if pUnkOuter <> null then invalidArg "pUnkOuter" "not null."
            if riid <> iuknown && riid <> typeof<IMFTransform>.GUID then invalidArg "riid" "not supportted."
            let activates = enumMFT MFT_CATEGORY_VIDEO_ENCODER outputType
            if activates.Length = 0 then Marshal.ThrowExceptionForHR (*MF_E_TOPO_CODEC_NOT_FOUND*)0xC00D5212
            let activate = activates |> Array.tryFind (fun activete -> activete.GetGUID(MFT_TRANSFORM_CLSID_Attribute).ToString() = preferredCodec)
            let activate = defaultArg activate activates.[0]
            let transform = activate.ActivateObject typeof<IMFTransform>.GUID :?> IMFTransform

            if subType = MFVideoFormat_WMV3 then
                let propertyStore = box transform :?> IPropertyStore
                //propertyStore.SetValue(ref MFPKEY_VBRENABLED, PROPVARIANT true |> ref)
                propertyStore.SetValue(ref MFPKEY_COMPRESSIONOPTIMIZATIONTYPE, PROPVARIANT 1 |> ref)
                propertyStore.SetValue(ref MFPKEY_NUMTHREADS, PROPVARIANT Environment.ProcessorCount |> ref)

            ppvObject <- transform
        member this.LockServer(fLock) =
            ()
    interface IDisposable with
        member this.Dispose () =
            MFTUnregisterLocal this

let test () =
    let supported = Version(6, 1) <= Environment.OSVersion.Version
    if supported then LoadLibrary(if IntPtr.Size = 8 then @"x64\d3d9.dll" else @"x86\d3d9.dll") |> ignore
    supported

let configList () =
    use __ = resource (fun () -> MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET)) MFShutdown
    let encoders category outputType =
        enumMFT category outputType |> Array.map (fun activate ->
            let friendlyNamePtr, _ = activate.GetAllocatedString(MFT_FRIENDLY_NAME_Attribute)
            let friendlyName = Marshal.PtrToStringUni friendlyNamePtr
            Marshal.FreeCoTaskMem friendlyNamePtr
            let clsid = activate.GetGUID(MFT_TRANSFORM_CLSID_Attribute)
            friendlyName, clsid)
    [| for KeyValue (key, (video, audio)) in table -> 
        let video = MFT_REGISTER_TYPE_INFO(MFMediaType_Video, video) |> encoders MFT_CATEGORY_VIDEO_ENCODER
        let audio = MFT_REGISTER_TYPE_INFO(MFMediaType_Audio, audio) |> encoders MFT_CATEGORY_AUDIO_ENCODER
        key, (video, audio) |]

let start folder (size : Size) save (stop : EventWaitHandle) (completed : EventWaitHandle) =
    async{
        let extension, videoFormat, audioFormat =
            match Settings.Default.Extension.ToLowerInvariant() |> table.TryGetValue with
            | true, (video, audio) -> Settings.Default.Extension, video, audio
            | false, _             -> "wmv", MFVideoFormat_WMV3, MFAudioFormat_WMAudioV9
        use _ = resource (fun () -> MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET)) MFShutdown
        use _ = new MFTransformClassFactory(MFMediaType_Video, videoFormat, Settings.Default.VideoCodec)
        use _ = new MFTransformClassFactory(MFMediaType_Audio, audioFormat, Settings.Default.AudioCodec)
        let sinkWriter =
            let filename = Path.Combine(folder, String.Format("艦これ-{0:yyyyMMdd-HHmmss}.{1}", DateTime.Now, extension))
            let mutable attribute = null
            MFCreateAttributes(&attribute, 3u)
            attribute.SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1u)
            attribute.SetUINT32(MF_SINK_WRITER_DISABLE_THROTTLING, 1u)
            attribute.SetUnknown(MF_SINK_WRITER_ASYNC_CALLBACK, { new IMFSinkWriterCallback with member __.OnMarker(dwStreamIndex, pvContext) = ()
                                                                                                 member __.OnFinalize(hrStatus) = completed.Set() |> ignore })
            let mutable sinkWriter = null
            MFCreateSinkWriterFromURL(filename, 0n, attribute, &sinkWriter)
            sinkWriter

        let createMediaType () =
            let mutable mediaType = null
            MFCreateMediaType(&mediaType)
            mediaType

        let createSample sampleTime =
            let mutable sample = null
            MFCreateSample(&sample)
            sample.SetSampleTime(sampleTime)
            sample

        let addBuffer (sample : IMFSample) length write =
            // currently, AVX register is 256bit = 32bytes. So use 32bytes alignment.
            let mutable buffer = null
            MFCreateAlignedMemoryBuffer(length, 31u, &buffer)
            let address, _, _ = buffer.Lock()
            write address
            buffer.Unlock()
            buffer.SetCurrentLength(length)
            sample.AddBuffer(buffer)

        let initializeVideo width height fps =
            let mediaType = createMediaType ()
            mediaType.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video)
            mediaType.SetGUID(MF_MT_SUBTYPE, videoFormat)
            MFSetAttributeSize(mediaType, MF_MT_FRAME_SIZE, width, height)
            MFSetAttributeRatio(mediaType, MF_MT_FRAME_RATE, fps, 1u)
            mediaType.SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive)
            mediaType.SetUINT32(MF_MT_AVG_BITRATE, VIDEO_BIT_RATE)
            if videoFormat = MFVideoFormat_H264 then
                mediaType.SetUINT32(MF_MT_MPEG2_PROFILE, eAVEncH264VProfile_Main)
                //mediaType.SetUINT32(MF_MT_MPEG2_LEVEL, eAVEncH264VLevel3)
            let videoIndex = sinkWriter.AddStream(mediaType)
            let mediaType = createMediaType ()
            mediaType.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video)
            mediaType.SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32)
            MFSetAttributeSize(mediaType, MF_MT_FRAME_SIZE, width, height)
            mediaType.SetUINT32(MF_MT_DEFAULT_STRIDE, width * 4u)
            MFSetAttributeRatio(mediaType, MF_MT_FRAME_RATE, fps, 1u)
            mediaType.SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive)
            sinkWriter.SetInputMediaType(videoIndex, mediaType, null)
            videoIndex

        let initializeAudio () =
            let deviceEnumerator = Activator.CreateInstance(Guid "BCDE0395-E52F-467C-8E3D-C4579291692E" |> Type.GetTypeFromCLSID) :?> IMMDeviceEnumerator
            let device = deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole)
            let audioClient = device.Activate(typeof<IAudioClient>.GUID, (*CLSCTX_ALL*)0x17u, NativePtr.ofNativeInt 0n) :?> IAudioClient
            let pwfx = audioClient.GetMixFormat()
            let wfx = NativePtr.read pwfx
            let mutable availableTypes = null
            MFTranscodeGetAudioOutputAvailableTypes(audioFormat, (*MFT_ENUM_FLAG_ALL*)0x0000003Fu, null, &availableTypes)
            let availableTypes = availableTypes
            // TODO: better selection
            let count = availableTypes.GetElementCount()
            let rec loop i =
                if i = count then failwith ""
                let mediaType = availableTypes.GetElement(i) :?> IMFMediaType
                let channels = MFGetAttributeUINT32(mediaType, MF_MT_AUDIO_NUM_CHANNELS, 0u)
                let samplesPerSec = MFGetAttributeUINT32(mediaType, MF_MT_AUDIO_SAMPLES_PER_SECOND, 0u)
                if channels = uint32 wfx.nChannels && samplesPerSec = wfx.nSamplesPerSec then
                    mediaType
                else
                    loop (i + 1u)
            let audioIndex = sinkWriter.AddStream(loop 0u)
            let mediaType = createMediaType ()
            MFInitMediaTypeFromWaveFormatEx(mediaType, pwfx, uint32 sizeof<WAVEFORMATEX> + uint32 wfx.cbSize)
            sinkWriter.SetInputMediaType(audioIndex, mediaType, null)
            audioClient.Initialize((*AUDCLNT_SHAREMODE_SHARED*)0, (AUDCLNT_STREAMFLAGS_NOPERSIST ||| AUDCLNT_STREAMFLAGS_LOOPBACK), TimeSpan.TicksPerSecond, 0L, pwfx, NativePtr.ofNativeInt 0n)
            let frameSize = uint32 wfx.wBitsPerSample / 8u * uint32 wfx.nChannels
            let captureClient = audioClient.GetService(typeof<IAudioCaptureClient>.GUID) :?> IAudioCaptureClient
            let captureAudio videoTimestamp =
                let mutable data, frames, flags, devicePosition, audioTimestamp = 0n, 0u, 0u, 0uL, 0uL
                captureClient.GetBuffer(&data, &frames, &flags, &devicePosition, &audioTimestamp) |> ignore
                while 0u < frames && int64 audioTimestamp < videoTimestamp do
                    captureClient.ReleaseBuffer(frames)
                    captureClient.GetBuffer(&data, &frames, &flags, &devicePosition, &audioTimestamp) |> ignore
                if 0u < frames then
                    let audioSample = int64 audioTimestamp - videoTimestamp |> createSample 
                    while 0u < frames do
                        let bufferSize = frames * frameSize
                        do
                            let data = data
                            addBuffer audioSample bufferSize (fun address ->
                                MFCopyImage(address, bufferSize, data, bufferSize, bufferSize, 1u))
                        captureClient.ReleaseBuffer(frames)
                        captureClient.GetBuffer(&data, &frames) |> ignore
                    sinkWriter.WriteSample(audioIndex, audioSample)
            audioClient, captureAudio

        let startTimestamp = ref 0L
        let audioClient, captureAudio = initializeAudio ()
        let videoStart, videoStop =
            if Settings.Default.FrameRate = 0 then
                // use Direct3D
                let mutable width, height, format, fps = 0u, 0u, D3DFORMAT.UNKNOWN, 0u
                GetParameter(&width, &height, &format, &fps)
                let width, height = width, height
                let videoIndex = initializeVideo width height fps
                let callback = Callback(fun timestamp obj ->
                    if !startTimestamp = 0L then startTimestamp := timestamp
                    let videoSample = timestamp - !startTimestamp |> createSample
                    let mutable buffer = null
                    MFCreateDXSurfaceBuffer(IID_IDirect3DSurface9, obj, false, &buffer)
                    buffer.SetCurrentLength(width * height * 4u)
                    videoSample.AddBuffer(buffer)
                    sinkWriter.WriteSample(videoIndex, videoSample)
                    captureAudio !startTimestamp)
                let handle = GCHandle.Alloc callback
                (fun () -> Start callback), (fun () -> Stop(); handle.Free())
            else
                // use GDI
                //
                // from Ks.h
                // Performs a x*y/z operation on 64 bit quantities by splitting the operation. The equation
                // is simplified with respect to adding in the remainder for the upper 32 bits.
                //
                // (xh * 10000000 / Frequency) * 2^32 + ((((xh * 10000000) % Frequency) * 2^32 + (xl * 10000000)) / Frequency)
                //
                let hns x =
                    let xh, xl = x >>> 32, uint32 x |> int64
                    (xh * TimeSpan.TicksPerSecond / Stopwatch.Frequency <<< 32) + ((xh * TimeSpan.TicksPerSecond % Stopwatch.Frequency <<< 32) + xl * TimeSpan.TicksPerSecond) / Stopwatch.Frequency
                let width = uint32 size.Width
                let height = uint32 size.Height
                let videoIndex = initializeVideo width height (uint32 Settings.Default.FrameRate)
                let videoTimer = new Timer(Interval = 1000 / Settings.Default.FrameRate)
                videoTimer.Tick.Add(fun _ ->
                    let timestamp = Stopwatch.GetTimestamp() |> hns
                    if !startTimestamp = 0L then startTimestamp := timestamp
                    let videoSample = timestamp - !startTimestamp |> createSample
                    addBuffer videoSample (width * height * 4u) (fun address ->
                        use bitmap = new Bitmap(size.Width, size.Height, size.Width * 4, PixelFormat.Format32bppRgb, address)
                        use graphics = Graphics.FromImage bitmap
                        graphics.GetHdc() |> save
                        graphics.ReleaseHdc())
                    sinkWriter.WriteSample(videoIndex, videoSample)
                    captureAudio !startTimestamp)
                videoTimer.Start, videoTimer.Dispose

        if true then    // create scope like 'do'
            use _ = resource sinkWriter.BeginWriting sinkWriter.Finalize
            use _ = resource audioClient.Start audioClient.Stop
            use _ = resource videoStart videoStop
            do! Async.AwaitWaitHandle stop |> Async.Ignore
        return! Async.AwaitWaitHandle completed |> Async.Ignore
    } |> Async.StartImmediate
