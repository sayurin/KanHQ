namespace Sayuri.Windows.Forms
open System
open System.Runtime.InteropServices
open System.Windows.Forms
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<Struct; StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)>]
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
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = (*MIXER_SHORT_NAME_CHARS*)16)>]
    val szShortName : string
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = (*MIXER_LONG_NAME_CHARS*)64)>]
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
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = (*MAXPNAMELEN*)32)>]
    val szPname : string
    new (dwComponentType) = { cbStruct = Marshal.SizeOf typeof<MIXERLINE>; dwComponentType = dwComponentType }

[<StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)>]
type MIXERCONTROL =
    val cbStruct : int  // 228 or 228
    [<DefaultValue>]
    val dwControlID : int
    [<DefaultValue>]
    val dwControlType : int
    [<DefaultValue>]
    val fdwControl : int
    [<DefaultValue>]
    val cMultipleItems : int
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = (*MIXER_SHORT_NAME_CHARS*)16)>]
    val szShortName : string
    [<DefaultValue; MarshalAs(UnmanagedType.ByValTStr, SizeConst = (*MIXER_LONG_NAME_CHARS*)64)>]
    val szName : string
    [<DefaultValue; MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)>]
    val Bounds_dwReserved : int[]
    [<DefaultValue; MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)>]
    val Metrics_dwReserved : int[]
    new () = { cbStruct = Marshal.SizeOf typeof<MIXERCONTROL> }

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

module NativeMethods =
    [<DllImport("Winmm.dll")>]
    extern int private mixerOpen([<Out>] nativeint& phmx, int uMxId, nativeint dwCallback, nativeint dwInstance, int fdwOpen);
    [<DllImport("Winmm.dll", CharSet = CharSet.Unicode)>]
    extern int private mixerGetLineInfo(nativeint hmxobj, MIXERLINE& pmxl, int fdwInfo);
    [<DllImport("Winmm.dll", CharSet = CharSet.Unicode)>]
    extern int private mixerGetLineControls(nativeint hmxobj, MIXERLINECONTROLS pmxlc, int fdwControls);
    [<DllImport("Winmm.dll")>]
    extern int private mixerGetControlDetails(nativeint hmxobj, MIXERCONTROLDETAILS pmxcd, int fdwDetails);
    [<DllImport("Winmm.dll")>]
    extern int private mixerSetControlDetails(nativeint hmxobj, MIXERCONTROLDETAILS pmxcd, int fdwDetails);
    [<DllImport("Winmm.dll")>]
    extern int private mixerClose(nativeint hmx);

open NativeMethods

type MuteCheckBox () as self =
    inherit CheckBox (AutoCheck = false)
    let mutable state = None

    let close () =
        Option.iter (fst >> mixerClose >> ignore) state
        state <- None

    let updateState hmx controlId =
        let ptr = NativePtr.stackalloc 1
        let mxcd = MIXERCONTROLDETAILS(controlId, sizeof<int>, NativePtr.toNativeInt ptr)
        if mixerGetControlDetails(hmx, mxcd, (*MIXER_GETCONTROLDETAILSF_VALUE*)0x00000000) <> 0 then () else
        self.Checked <- NativePtr.read ptr <> 0

    override this.OnHandleCreated(e) =
        close ()
        let mutable hmx = 0n
        if mixerOpen(&hmx, 0, this.Handle, 0n, (*CALLBACK_WINDOW*)0x00010000l) <> 0 then () else

        let mutable mxl = MIXERLINE((*MIXERLINE_COMPONENTTYPE_SRC_WAVEOUT*)0x00001008)
        if mixerGetLineInfo(hmx, &mxl, (*MIXER_GETLINEINFOF_COMPONENTTYPE*)0x00000003) <> 0 then mixerClose hmx |> ignore else

        let mxctrl = MIXERCONTROL()
        let ptr = NativePtr.stackalloc<byte> mxctrl.cbStruct |> NativePtr.toNativeInt
        Marshal.StructureToPtr(mxctrl, ptr, false)
        let mxlc = MIXERLINECONTROLS(mxl.dwLineID, (*MIXERCONTROL_CONTROLTYPE_MUTE*)0x20010002, mxctrl.cbStruct, ptr)
        if mixerGetLineControls(hmx, mxlc, (*MIXER_GETLINECONTROLSF_ONEBYTYPE*)0x00000002) <> 0 then mixerClose hmx |> ignore else

        Marshal.PtrToStructure(ptr, mxctrl)
        updateState hmx mxctrl.dwControlID
        state <- Some (hmx, mxctrl.dwControlID)

    override __.Dispose(disposing) =
        close ()
        base.Dispose disposing

    override this.OnClick(e) =
        state |> Option.iter (fun (hmx, controlId) ->
            let ptr = NativePtr.stackalloc 1
            NativePtr.write ptr (if not this.Checked then 1 else 0)
            let mxcd = MIXERCONTROLDETAILS(controlId, sizeof<int>, NativePtr.toNativeInt ptr)
            mixerSetControlDetails(hmx, mxcd, (*MIXER_SETCONTROLDETAILSF_VALUE*)0x00000000) |> ignore)

    override __.WndProc(m) =
        match m.Msg with
        | (*MM_MIXM_LINE_CHANGE   *)0x3D0 -> ()
        | (*MM_MIXM_CONTROL_CHANGE*)0x3D1 -> let m = m in Option.iter (fun (hmx, controlId) -> if m.WParam = hmx && int m.LParam = controlId then updateState hmx controlId) state
        | _                               -> base.WndProc(&m)
