import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../protocol-compat.js", import.meta.url), "utf8");
new vm.Script(source);

const context = {
  desktopState: { isDesktop: true, bridge: false, token: "" },
  desktopBusy: false,
  detectEnvironment() {},
  toggle() {},
  renderEnvironment() {},
  show(_id, message) { context.message = message; },
  async companionRequest() { return context.health; },
  Set, Number, String, Boolean, Error
};
vm.createContext(context);
vm.runInContext("var checkCompanion, autoConnectCompanion;", context);
vm.runInContext(source, context);

const results = [];
for (const protocol of [2, 3, 4]) {
  context.health = { product: "Excel Data Assistant Companion", version: "0.9.0", protocol, sessionToken: "token" };
  results.push([protocol, await context.checkCompanion(false), context.desktopState.protocol]);
}
if (JSON.stringify(results) !== JSON.stringify([[2, true, 2], [3, true, 3], [4, false, 4]])) {
  throw new Error(`Unexpected protocol matrix: ${JSON.stringify(results)}`);
}
console.log("PASS: companion protocols 2 and 3 accepted; unknown protocol rejected");
export const report = { accepted: [2, 3], rejected: [4] };
