## Soundboard Virtual Microphone (Driver)

This repository’s WPF app can output audio to any **render** endpoint. To make other apps see that audio as a **microphone**, Windows needs a **virtual audio device**.

You requested that the project **creates its own virtual microphone** named **Soundboard**. On Windows, that means shipping/installing a **kernel-mode audio driver** (or equivalent virtual endpoint). This cannot be done from .NET/WPF user-mode code alone.

### Current status in this repo
- The app is implemented and can output to a selected output device.
- This repo includes **instructions** to build a SysVAD-based virtual audio device, but **building/signing requires the Windows Driver Kit (WDK) + Visual Studio** which are not currently installed on this machine.

### Required tools (install these first)
- Visual Studio 2022 (Desktop development with C++)
- Windows 11 WDK (includes `inf2cat`, `signtool`, etc.)

After install, verify these are available in a **Developer PowerShell for VS**:
- `msbuild`
- `inf2cat`
- `signtool`
- `stampinf`

### Recommended driver base: Microsoft SysVAD sample
Use Microsoft’s **SysVAD** sample as the starting point and modify it to expose:
- A render endpoint named `Soundboard (Virtual Cable Input)` (the app outputs here)
- A capture endpoint named `Soundboard (Virtual Cable Output)` (other apps select this as “mic”)

High-level steps:
1. Get SysVAD sample from the WDK samples.
2. Rename the device/interface strings to `Soundboard ...`.
3. Build driver in **Test Signing** mode first.
4. Create/adjust the `.inf` to install the endpoints.
5. Generate a catalog (`inf2cat`) and sign (`signtool`) for test install.
6. Install driver (admin) and confirm both endpoints appear in Windows Sound settings.

### How the app will use it
Once installed, the app will automatically prefer an output device whose name contains **Soundboard** (and still lets you choose manually).

