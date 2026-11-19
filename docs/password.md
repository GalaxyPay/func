# Password

The FUNC service runs with administrator privileges and manages services on your computer, so it asks you to choose a password the first time you open the app. This stops websites you visit and other machines on your network from controlling your node.

::: warning Choose it right after installing
Until you choose a password, anyone who can reach the app could choose it for you.
:::

The app can be opened from any device that can reach the computer (see [Remote Access](/remote-access)), so a computer without a screen is set up by opening `http://<its address>:3536` from another computer.

Signing in keeps you signed in on that browser for 30 days. You can change the password, or sign out, from [Settings](/settings). Changing it signs out every other browser and device.

Anyone with the password can control the node services on your computer, so pick a strong one and don't share it. Let your browser or phone save it: FUNC's sign-in works with password managers.

## Sign in with your wallet

Once your node has participation keys, you can sign in with the wallet that holds the participating account instead of typing the password. On the sign-in page, choose **Sign in with Lute** or **Sign in with Pera** and approve the request in your wallet. Any account with participation keys on this computer can sign in this way. If the account is rekeyed, sign with the account it's rekeyed to.

::: info Voi
Wallet sign-in is for Algorand. Participation keys on a Voi node don't let an account sign in, and only Algorand rekeys count: a Voi rekey doesn't change who signs.
:::

Wallet sign-in needs HTTPS, so open FUNC at `https://<computer's address>:3537` (see [Remote Access](/remote-access)). It's only offered when your node is running and has participation keys, so keep your password too.

Your wallet signs for the address you opened FUNC at, and FUNC only accepts its computer's own name and IP addresses. If you open it by another name, such as a DNS name you set up, list that name and port in `func.json` in the folder that holds the password file (see below):

```json
{ "Auth": { "Hosts": ["node.home.lan:3537"] } }
```

## Sign-in limits

After five failed sign-ins from one address, that address must wait 30 seconds, doubling with each further failure. The whole network is also limited to ten failures every ten minutes. Sign-ins on the computer itself are never blocked by the network-wide limit, so you can always sign in there.

## Forgot your password?

Any of these lets you choose a new one.

### With your wallet

Sign in with your wallet (see above), open [Settings](/settings), choose **Change** next to Password, then **Forgot it? Confirm with your wallet instead**.

### From the tray (Windows and Mac)

Choose **Reset Password…** from the FUNC icon in the system tray (Windows) or menu bar (Mac), and approve the administrator prompt. FUNC opens in your browser, ready for you to choose a new password.

### From a terminal

On Linux, or anywhere without the tray, run:

```sh
sudo /opt/func/FUNC --reset-password
```

On Windows, run `FUNC.exe --reset-password` from an administrator terminal, in the folder FUNC is installed in.

Then open FUNC and choose a new password.

::: warning Choose the new password right away
Resetting signs out every browser and device. Until you choose a new password, anyone who can reach the app could choose it for you.
:::

### By deleting the password file

If none of the above works, delete the file below as an administrator, then reload FUNC in your browser.

| Computer | Password file |
| --- | --- |
| Windows | `C:\ProgramData\func\auth.json` |
| Mac | `/usr/local/share/func/auth.json` |
| Linux | `/usr/share/func/auth.json` |
