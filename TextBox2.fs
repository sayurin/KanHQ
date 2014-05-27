namespace Sayuri.Windows.Forms
open System.Runtime.InteropServices
open System.Windows.Forms

type TextBox2 () =
    inherit TextBox ()
    
    [<DllImport "Imm32.dll">]
    static extern nativeint ImmGetContext(nativeint hWnd)
    [<DllImport "Imm32.dll">]
    static extern bool ImmGetOpenStatus(nativeint hIMC)
    [<DllImport "Imm32.dll">]
    static extern bool ImmNotifyIME(nativeint hIMC, uint32 dwAction, uint32 dwIndex, uint32 dwValue)
    [<DllImport "Imm32.dll">]
    static extern bool ImmReleaseContext(nativeint hWnd, nativeint hIMC)
    [<Literal>]
    static let NI_SELECTCANDIDATESTR = 0x0015u
    [<Literal>]
    static let CPS_COMPLETE = 0x0001u

    override this.OnLostFocus e =
        let context = ImmGetContext this.Handle
        if context <> 0n then
            if ImmGetOpenStatus context then
                ImmNotifyIME(context, NI_SELECTCANDIDATESTR, CPS_COMPLETE, 0u) |> ignore
            ImmReleaseContext(this.Handle, context) |> ignore
        base.OnLostFocus e
