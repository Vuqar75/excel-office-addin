// Compatibility adapter for Companion 0.9.0. Protocol 2 remains wire-compatible
// with the hardened 3.2 web client; no command or payload shape changed.
const companionRequestProtocol2 = companionRequest;
companionRequest = async function(path, options = {}) {
  const response = await companionRequestProtocol2(path, options);
  if (path === "health" && Number(response?.protocol) === 2) {
    return { ...response, protocol: 3, wireProtocol: 2 };
  }
  return response;
};
