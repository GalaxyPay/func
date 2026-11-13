# Manage Menu

The **Manage** button under the Status panel holds every action for your node. Each option only appears when it applies.

## Node

### Create Service

Creates and starts a background service to run the node. This is the same as **Set Up Node** in Getting Started. Only available when the service doesn't exist.

### Start Node

Starts the node's service. Only available when the node is stopped.

### Stop Node

Stops the node's service. Only available when the node is running.

### Remove Service

Removes the node's service, so the node no longer starts with your computer. Your node data, including participation keys, is kept. Only available when the node is stopped.

### Configure

Changes your node's port, DNS bootstrap, log level (BaseLoggerDebugLevel) and P2P setting.

### View Log

Shows the node's log, filtered to warnings and errors by default. You can search it, or follow it as new entries are written. Only available when the service exists.

### Enable / Disable Telemetry

Links your node to the [Nodely Telemetry](/telemetry) service. Once enabled, **Open Nodely Telemetry** opens your node's dashboard on Nodely. Only available on Algorand while the node is running.

### Node Data Directory

Changes where the node's data is stored. FUNC needs permission to write to the new location. Only available when the service doesn't exist.

### Delete Node Data

Deletes all of the node's data, **including participation and KMD keys**. Only available when the service doesn't exist.

## Reti / Valar

Adds, starts, stops or removes a [Reti](/reti) or [Valar](/valar) staking service alongside your node. Adding one is only available on Algorand while the node is running.

Once a service is installed, its light shows in the Status panel, with an **Update** button when a newer version is available.
