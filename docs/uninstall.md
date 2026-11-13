# Uninstall

FUNC manages your node's service. Uninstalling it does **not** remove your node data by default. To also remove your node data, including participation and KMD keys, follow the "purge" step for your computer below.

## Windows

1. To remove everything, first use the app's [Manage menu](/manage) to **Remove Service**, then **Delete Node Data**.
2. Open **Settings > Apps > Installed apps**, search for **FUNC**, and choose **Uninstall**.

## Mac

An uninstall script comes with the app. In Terminal, run one of:

```sh
sudo /opt/func/uninstall.sh           # remove the app, keep node data
sudo /opt/func/uninstall.sh --purge   # also remove node data and service accounts
```

## Linux

FUNC is a standard package, so use your package manager:

```sh
sudo apt remove func    # remove the app, keep node data
sudo apt purge func     # also remove node data and service accounts
```
