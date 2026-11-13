# Existing Nodes (Mac / Linux)

FUNC detects an Algorand node that was installed without it:

- the `algorand` apt/rpm package, installed by hand or by [NodeKit](https://nodekit.run)
- NodeKit's Homebrew install on Mac
- go-algorand's `update.sh`

FUNC won't create its own Algorand service alongside one of these. If FUNC doesn't have an Algorand node of its own, it manages an existing mainnet node automatically, where it is. NodeKit and `goal` keep working with it. If the node is later removed outside FUNC (for example with `apt remove algorand` or `nodekit uninstall`), FUNC stops managing it.

How FUNC's options work with an existing node:

- **Start / Stop Node** controls the node's existing service: `algorand` on Linux, `com.algorand.algod` on Mac, or `goal node start/stop` if it has no service.
- **Update** in Settings uses whatever installed the node: `apt`, `dnf`, `brew` or `update.sh`. Only the latest version is offered.
- **Node Data Directory** moves the data and leaves a link at the old path. The service, NodeKit and `goal` keep finding the node there without changes, including any `-d` or `ALGORAND_DATA` you already use.
- **Delete Node Data** deletes the node's data, including participation keys, and uninstalls the existing node (for example `apt-get remove algorand`). FUNC can then create its own node.
