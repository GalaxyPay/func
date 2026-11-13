# Password

The FUNC service runs with administrator privileges and manages services on your computer, so it asks you to choose a password the first time you open the app. This stops websites you visit and other machines on your network from controlling your node.

::: warning Choose it right after installing
Until you choose a password, anyone who can reach the app could choose it for you.
:::

The app can be opened from any device that can reach the computer (see [Remote Access](/remote-access)), so a computer without a screen is set up by opening `http://<its address>:3536` from another computer.

Signing in keeps you signed in on that browser for 30 days. You can change the password, or sign out, from [Settings](/settings). Changing it signs out every other browser and device.

Anyone with the password can control the node services on your computer, so pick a strong one and don't share it.

## Sign-in limits

After five failed sign-ins from one address, that address must wait 30 seconds, doubling with each further failure. The whole network is also limited to ten failures every ten minutes. Sign-ins on the computer itself are never blocked by the network-wide limit, so you can always sign in there.

## Forgot your password?

Delete the file below and restart the FUNC service (or restart the computer). The app then asks you to choose a new password.

| Computer | Password file |
| --- | --- |
| Windows | `C:\ProgramData\func\auth.json` |
| Mac | `/usr/local/share/func/auth.json` |
| Linux | `/usr/share/func/auth.json` |
