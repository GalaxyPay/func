# Settings

Open Settings with the gear icon at the top right. A dot on the icon means there's something to update: a new version of FUNC, a new node version (in **Notify** mode), or a node update that's required.

| Setting | What it does |
| --- | --- |
| **Password** | Change your password, or sign out of this browser. |
| **FUNC Version** | The installed version of FUNC. An **Update** button appears when a newer version is available. |
| **Node Version** | The installed version of the node software, with an **Update** button when there's a newer one. In **Manual** mode, a menu lets you pick any release instead. |
| **Node Updates** | How the node software is kept up to date: **Auto**, **Notify** or **Manual**. See [Node updates](#node-updates). |
| **Diagnostics** | Copy or download troubleshooting info to share when asking for help. See [Getting Help](/support). |
| **Show Machine Name** | Shows the computer's name in the top bar. Handy if you run more than one node. |
| **Show Advanced Details** | Adds node health charts, algod's version and uptime, and a list of peers to the dashboard. |
| **Show Alternative Networks** | Adds tabs for other networks, such as Voi. |

Announcements about FUNC appear under the bell icon beside the gear.

## Node updates

| Mode | What it does |
| --- | --- |
| **Auto** | Installs each new node release on its own. FUNC checks shortly after it starts, then every few hours. The node stops briefly while it updates. If an update fails, the reason shows in Settings. |
| **Notify** | The default. Alerts you when a new release is out, and you choose when to install it with **Update**. |
| **Manual** | Stays on the version you pick, with no new-version alerts. Choose a version from the menu beside **Node Version**. |

Installing an older version switches you to **Manual**, so FUNC doesn't replace it.

If you run an [existing node](/existing-nodes), its own installer updates it, so this setting isn't shown.

### Required updates

Sometimes the Algorand network approves an upgrade to its consensus protocol. A node that doesn't support it stops syncing (stalls) at the upgrade round. When that applies to your node, **Node Version** shows a warning in red, whatever the update mode. Update the node before the upgrade round.

In the version menu, releases that can't follow the network are greyed out and can't be installed.
