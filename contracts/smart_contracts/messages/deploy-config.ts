import { AlgorandClient } from "@algorandfoundation/algokit-utils";
import { MessagesFactory } from "../artifacts/messages/MessagesClient";

// Below is a showcase of various deployment options you can use in TypeScript Client
export async function deploy() {
  console.log("=== Deploying Messages ===");

  const algorand = AlgorandClient.fromEnvironment();
  const deployer = await algorand.account.fromEnvironment("DEPLOYER");

  const factory = algorand.client.getTypedAppFactory(MessagesFactory, {
    defaultSender: deployer.addr,
  });

  const { appClient, result } = await factory.deploy({
    onUpdate: "append",
    onSchemaBreak: "append",
  });

  // If app was just created fund the app account
  if (["create", "replace"].includes(result.operationPerformed)) {
    await algorand.send.payment({
      amount: (1000).microAlgo(),
      sender: deployer.addr,
      receiver: appClient.appAddress,
    });
  }
}
