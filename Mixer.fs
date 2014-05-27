module Sayuri.Mixer
open System
open System.Runtime.InteropServices

#nowarn "9"

[<Literal>]
let MAXPNAMELEN = 32 
[<Literal>]
let MIXER_SHORT_NAME_CHARS = 16
[<Literal>]
let MIXER_LONG_NAME_CHARS = 64
[<Literal>]
let MIXER_GETLINECONTROLSF_ONEBYTYPE = 0x00000002
[<Literal>]
let MIXERLINE_COMPONENTTYPE_DST_FIRST = 0x00000000
let MIXERLINE_COMPONENTTYPE_DST_SPEAKERS = MIXERLINE_COMPONENTTYPE_DST_FIRST + 4
[<Literal>]
let MIXERLINE_COMPONENTTYPE_SRC_FIRST = 0x00001000
let MIXERLINE_COMPONENTTYPE_SRC_WAVEOUT = MIXERLINE_COMPONENTTYPE_SRC_FIRST + 8

[<Literal>]
let MIXER_GETLINEINFOF_COMPONENTTYPE = 0x00000003
[<Literal>]
let MIXERCONTROL_CT_CLASS_SWITCH = 0x20000000
[<Literal>]
let MIXERCONTROL_CT_SC_SWITCH_BOOLEAN = 0x00000000
[<Literal>]
let MIXERCONTROL_CT_UNITS_BOOLEAN = 0x00010000
[<Literal>]
let MIXERCONTROL_CONTROLTYPE_BOOLEAN = MIXERCONTROL_CT_CLASS_SWITCH ||| MIXERCONTROL_CT_SC_SWITCH_BOOLEAN ||| MIXERCONTROL_CT_UNITS_BOOLEAN
let MIXERCONTROL_CONTROLTYPE_MUTE = MIXERCONTROL_CONTROLTYPE_BOOLEAN + 2
[<Literal>]
let MIXER_SETCONTROLDETAILSF_VALUE = 0x00000000

[<StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)>]
type MIXERLINE =
    val cbStruct : int  // 280 or 284
    [<DefaultValue>]
    val dwDestination : int
    [<DefaultValue>]
    val dwSource : int
    [<DefaultValue>]
    val dwLineID : int
    [<DefaultValue>]
    val fdwLine : int
    [<DefaultValue>]
    val dwUser : nativeint
    val dwComponentType : int
    [<DefaultValue>]
    val cChannels : int
    [<DefaultValue>]
    val cConnections : int
    [<DefaultValue>]
    val cControls : int
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = MIXER_SHORT_NAME_CHARS)>]
    val szShortName : string
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = MIXER_LONG_NAME_CHARS)>]
    val szName : string
    // Target
    [<DefaultValue>]
    val dwType : int
    [<DefaultValue>]
    val dwDeviceID : int
    [<DefaultValue>]
    val wMid : int16
    [<DefaultValue>]
    val wPid : int16
    [<DefaultValue>]
    val vDriverVersion : int
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAXPNAMELEN)>]
    val szPname : string
    new (dwComponentType) = { cbStruct = Marshal.SizeOf typeof<MIXERLINE>; dwComponentType = dwComponentType }

[<StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)>]
type MIXERCONTROL =
    [<DefaultValue>]
    val cbStruct : int  // 228 or 228
    [<DefaultValue>]
    val dwControlID : int
    [<DefaultValue>]
    val dwControlType : int
    [<DefaultValue>]
    val fdwControl : int
    [<DefaultValue>]
    val cMultipleItems : int
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = MIXER_SHORT_NAME_CHARS)>]
    val szShortName : string
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = MIXER_LONG_NAME_CHARS)>]
    val szName : string
    [<DefaultValue; MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)>]
    val Bounds_dwReserved : int[]
    [<DefaultValue; MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)>]
    val Metrics_dwReserved : int[]
    new () = {}

[<StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)>]
type MIXERLINECONTROLS =
    val cbStruct : int  // 24 or 28
    val dwLineID : int
    val dwControlType : int
    val cControls : int
    val cbmxctrl : int
    val pamxctrl : nativeint
    new (dwLineID, dwControlType, cbmxctrl, pamxctrl) = { cbStruct = Marshal.SizeOf typeof<MIXERLINECONTROLS>; dwLineID = dwLineID; dwControlType = dwControlType; cControls = 1; cbmxctrl = cbmxctrl; pamxctrl = pamxctrl }

[<StructLayout(LayoutKind.Sequential, Pack = 4)>]
type MIXERCONTROLDETAILS =
    val cbStruct : int  // 24 or 32
    val dwControlID : int
    val cChannels : int
    val cMultipleItems : nativeint  // for union member of HWND hwndOwner.
    val cbDetails : int
    val paDetails : nativeint
    new (dwControlID, cbDetails, paDetails) = { cbStruct = Marshal.SizeOf typeof<MIXERCONTROLDETAILS>; dwControlID = dwControlID; cChannels = 1; cMultipleItems = 0n; cbDetails = cbDetails; paDetails = paDetails }

[<DllImport("Winmm.dll")>]
extern int mixerOpen([<param: Out>] nativeint& phmx, int uMxId, nativeint dwCallback, nativeint dwInstance, int fdwOpen);
[<DllImport("Winmm.dll", CharSet = CharSet.Unicode)>]
extern int mixerGetLineInfo(nativeint hmxobj, [<param: In; Out>] MIXERLINE pmxl, int fdwInfo);
[<DllImport("Winmm.dll", CharSet = CharSet.Unicode)>]
extern int mixerGetLineControls(nativeint hmxobj, [<param: In; Out>] MIXERLINECONTROLS pmxlc, int fdwControls);
[<DllImport("Winmm.dll")>]
extern int mixerSetControlDetails(nativeint hmxobj, MIXERCONTROLDETAILS pmxcd, int fdwDetails);
[<DllImport("Winmm.dll")>]
extern int mixerClose(nativeint hmx);

let mute b =
    let mutable hmx = 0n
    let result = mixerOpen(&hmx, 0, 0n, 0n, 0)
    if result <> 0 then () else

    try
        let mutable mxl = MIXERLINE MIXERLINE_COMPONENTTYPE_SRC_WAVEOUT
        let result = mixerGetLineInfo(hmx, mxl, MIXER_GETLINEINFOF_COMPONENTTYPE)
        if result <> 0 then () else

        let cbmxctrl = Marshal.SizeOf typeof<MIXERCONTROL>
        let ptr = Marshal.AllocHGlobal cbmxctrl
        try
            Marshal.WriteInt32(ptr, cbmxctrl)
            let mutable mxlc = MIXERLINECONTROLS(mxl.dwLineID, MIXERCONTROL_CONTROLTYPE_MUTE, cbmxctrl, ptr)
            let result = mixerGetLineControls(hmx, mxlc, MIXER_GETLINECONTROLSF_ONEBYTYPE)
            if result <> 0 then () else

            let mxctrl = Marshal.PtrToStructure(ptr, typeof<MIXERCONTROL>) :?> MIXERCONTROL
            Marshal.WriteInt32(ptr, if b then 1 else 0)
            let mxcd = MIXERCONTROLDETAILS(mxctrl.dwControlID, 4, ptr)
            mixerSetControlDetails(hmx, mxcd, MIXER_SETCONTROLDETAILSF_VALUE) |> ignore
        finally
            Marshal.FreeHGlobal ptr
    finally
        mixerClose hmx |> ignore
