# Remote Access

You can use FUNC from your phone or another computer on your network, with the same [password](/password). To do that, open these ports on the computer running FUNC:

| Port | Used for |
| --- | --- |
| 3536 and 3537 | FUNC's web page and API |
| 8081 | Algorand node (algod) |
| 8082 and 3538 | Voi node (algod) |

You only need the node ports for the networks you use, and you can change them under **Manage > Configure**.

::: danger Local network only
Only open these ports on your local network. **Do not** open them to the internet.
:::

Use port **3537** when opening FUNC from another device: `https://<computer's address>:3537`. It serves the page over HTTPS with a self-signed certificate, so your password isn't sent in the clear. Your browser will warn about the certificate the first time; that's expected. Port 3537 is also required for WalletConnect wallets (such as Defly and Pera) and for the copy buttons.
