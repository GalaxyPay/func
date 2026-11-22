# Remote Access

You can use FUNC from your phone or another computer on your network, with the same [password](/password). To do that, open **one** port on the computer running FUNC:

| Port | Used for |
| --- | --- |
| 3537 | FUNC over HTTPS (recommended) |
| 3536 | FUNC over plain HTTP |

The page reaches your node through FUNC itself, so the node's own port (8081, or 8082 for Voi) and port 3538 don't need to be open. FUNC still listens on 3538, and the node on its own port, for tools that connect to them directly.

::: danger Local network only
Only open these ports on your local network. **Do not** open them to the internet.
:::

Use port **3537** when opening FUNC from another device: `https://<computer's address>:3537`. It serves the page over HTTPS with a self-signed certificate, so your password and node token aren't sent in the clear. Your browser will warn about the certificate the first time; that's expected. HTTPS is also required for WalletConnect wallets (such as Defly and Pera) and for the copy buttons.

## Behind a reverse proxy

To avoid the certificate warning, you can put a reverse proxy with a trusted certificate in front of FUNC, for example [Caddy](https://caddyserver.com) with its local certificate authority, a Let's Encrypt certificate, or Tailscale Serve. Point it at FUNC's plain HTTP port on the same computer:

```caddy
func.example.lan {
    tls internal                     # or a real certificate
    reverse_proxy 127.0.0.1:3536
}
```

Everything, including the node, is then reached at that one address. The same local-network-only rule applies.

The dashboard waits up to about a minute for each new block. If your proxy has a short read timeout (for example nginx's `proxy_read_timeout`), set it to at least 70 seconds.
