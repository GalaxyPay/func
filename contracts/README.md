# func-notify

This project has been generated using AlgoKit. See below for default getting started instructions.

# Setup

### Pre-requisites

- [Nodejs 22](https://nodejs.org/en/download) or later
- [AlgoKit CLI 2.5](https://github.com/algorandfoundation/algokit-cli?tab=readme-ov-file#install) or later
- [Docker](https://www.docker.com/) (only required for LocalNet)
- [Puya Compiler 4.4.4](https://pypi.org/project/puyapy/) or later

> For interactive tour over the codebase, download [vsls-contrib.codetour](https://marketplace.visualstudio.com/items?itemName=vsls-contrib.codetour) extension for VS Code, then open the [`.codetour.json`](./.tours/getting-started-with-your-algokit-project.tour) file in code tour extension.

### Initial Setup

#### 1. Clone the Repository
Start by cloning this repository to your local machine.

#### 2. Install Pre-requisites
Ensure the following pre-requisites are installed and properly configured:

- **Docker**: Required for running a local Algorand network.
- **AlgoKit CLI**: Essential for project setup and operations. Verify installation with `algokit --version`, expecting `2.6.0` or later.

#### 3. Bootstrap Your Local Environment
Run the following commands within the project folder:

- **Setup Project**: Execute `algokit project bootstrap all` to install dependencies and setup npm dependencies.
- **Configure environment**: Execute `algokit generate env-file -a target_network localnet` to create a `.env.localnet` file with default configuration for `localnet`.
- **Start LocalNet**: Use `algokit localnet start` to initiate a local Algorand network.

### Development Workflow

#### Terminal
Directly manage and interact with your project using AlgoKit commands:

1. **Build Contracts**: `algokit project run build` compiles all smart contracts. You can also specify a specific contract by passing the name of the contract folder as an extra argument.
For example: `algokit project run build -- hello_world` will only build the `hello_world` contract.
2. **Deploy**: Use `algokit project deploy localnet` to deploy contracts to the local network. You can also specify a specific contract by passing the name of the contract folder as an extra argument.
For example: `algokit project deploy localnet -- hello_world` will only deploy the `hello_world` contract.

#### VS Code 
For a seamless experience with breakpoint debugging and other features:

1. **Open Project**: In VS Code, open the repository root.
2. **Install Extensions**: Follow prompts to install recommended extensions.
3. **Debugging**:
   - Use `F5` to start debugging.

#### JetBrains IDEs
While primarily optimized for VS Code, JetBrains IDEs are supported:

1. **Open Project**: In your JetBrains IDE, open the repository root.
2. **Automatic Setup**: The IDE should configure the Node.js environment.
3. **Debugging**: Use `Shift+F10` or `Ctrl+R` to start debugging. Note: Windows users may encounter issues with pre-launch tasks due to a known bug. See [JetBrains forums](https://youtrack.jetbrains.com/issue/IDEA-277486/Shell-script-configuration-cannot-run-as-before-launch-task) for workarounds.

## AlgoKit Workspaces and Project Management
This project supports both standalone and monorepo setups through AlgoKit workspaces. Leverage [`algokit project run`](https://github.com/algorandfoundation/algokit-cli/blob/main/docs/features/project/run.md) commands for efficient monorepo project orchestration and management across multiple projects within a workspace.

## AlgoKit Generators

This template provides a set of [algokit generators](https://github.com/algorandfoundation/algokit-cli/blob/main/docs/features/generate.md) that allow you to further modify the project instantiated from the template to fit your needs, as well as giving you a base to build your own extensions to invoke via the `algokit generate` command.

### Generate Smart Contract 

By default the template creates a single `HelloWorld` contract under notifications folder in the `smart_contracts` directory. To add a new contract:

1. From the root of the project (`../`) execute `algokit generate smart-contract`. This will create a new starter smart contract and deployment configuration file under `{your_contract_name}` subfolder in the `smart_contracts` directory.
2. Each contract potentially has different creation parameters and deployment steps. Hence, you need to define your deployment logic in `deploy-config.ts` file.
3. Technically, you need to reference your contract deployment logic in the `index.ts` file. However, by default, `index.ts` will auto import all TypeScript deployment files under `smart_contracts` directory. If you want to manually import specific contracts, modify the default code provided by the template in `index.ts` file.

> Please note, above is just a suggested convention tailored for the base configuration and structure of this template. The default code supplied by the template in the `index.ts` file is tailored for the suggested convention. You are free to modify the structure and naming conventions as you see fit.

### Generate '.env' files

By default the template instance does not contain any env files to deploy to different networks. Using [`algokit project deploy`](https://github.com/algorandfoundation/algokit-cli/blob/main/docs/features/project/deploy.md) against `localnet` | `testnet` | `mainnet` will use default values for `algod` and `indexer` unless overwritten via `.env` or `.env.{target_network}`. 

To generate a new `.env` or `.env.{target_network}` file, run `algokit generate env-file`

### Debugging Smart Contracts

This project is optimized to work with AlgoKit AVM Debugger extension. To activate it:

Refer to the commented header in the `index.ts` file in the `smart_contracts` folder.Since you have opted in to include VSCode launch configurations in your project, you can also use the `Debug TEAL via AlgoKit AVM Debugger` launch configuration to interactively select an available trace file and launch the debug session for your smart contract.


For information on using and setting up the `AlgoKit AVM Debugger` VSCode extension refer [here](https://github.com/algorandfoundation/algokit-avm-vscode-debugger). To install the extension from the VSCode Marketplace, use the following link: [AlgoKit AVM Debugger extension](https://marketplace.visualstudio.com/items?itemName=algorandfoundation.algokit-avm-vscode-debugger).

# Tools

This project makes use of Algorand TypeScript to build Algorand smart contracts. The following tools are in use:

- [Algorand](https://www.algorand.com/) - Layer 1 Blockchain; [Developer portal](https://dev.algorand.co/), [Why Algorand?](https://dev.algorand.co/getting-started/why-algorand/)
- [AlgoKit](https://github.com/algorandfoundation/algokit-cli) - One-stop shop tool for developers building on the Algorand network; [docs](https://github.com/algorandfoundation/algokit-cli/blob/main/docs/algokit.md), [intro tutorial](https://github.com/algorandfoundation/algokit-cli/blob/main/docs/tutorials/intro.md)
- [Algorand TypeScript](https://github.com/algorandfoundation/puya-ts/) - A semantically and syntactically compatible, typed TypeScript language that works with standard TypeScript tooling and allows you to express smart contracts (apps) and smart signatures (logic signatures) for deployment on the Algorand Virtual Machine (AVM); [docs](https://github.com/algorandfoundation/puya-ts/), [examples](https://github.com/algorandfoundation/puya-ts/tree/main/examples)
- [AlgoKit Utils](https://github.com/algorandfoundation/algokit-utils-ts) - A set of core Algorand utilities that make it easier to build solutions on Algorand.
- [NPM](https://www.npmjs.com/): TypeScript packaging and dependency management.
- [TypeScript](https://www.typescriptlang.org/): Strongly typed programming language that builds on JavaScript
- [ts-node-dev](https://github.com/wclr/ts-node-dev): TypeScript development execution environment


It has also been configured to have a productive dev experience out of the box in [VS Code](https://code.visualstudio.com/), see the [.vscode](./.vscode) folder.



