// Companion 0.9.0 intentionally keeps wire protocol 2 so installed 3.1 clients
// and the 3.2 web client can coexist during the update. Accept only known,
// explicitly compatible protocol versions here.
const COMPATIBLE_COMPANION_PROTOCOLS = new Set([2, 3]);
checkCompanion = async function(silent = false) {
  detectEnvironment();
  if (!desktopState.isDesktop) {
    if (!silent) show("desktop-result", "Локальный мост проверяется только в настольном Excel.");
    return false;
  }
  if (!silent) toggle(true);
  try {
    desktopState.token = "";
    const info = await companionRequest("health");
    const protocol = Number(info?.protocol) || 0;
    desktopState.protocol = protocol;
    desktopState.bridgeVersion = String(info?.version || "—");
    desktopState.uptime = Number(info?.uptimeSeconds) || 0;
    desktopState.excelRunning = Boolean(info?.excelRunning);
    if (info?.product !== "Excel Data Assistant Companion") {
      throw new Error("Получен ответ неизвестного локального сервиса.");
    }
    if (!COMPATIBLE_COMPANION_PROTOCOLS.has(protocol)) {
      throw new Error(`Несовместимый протокол локального моста: ${protocol || "не указан"}.`);
    }
    desktopState.bridge = true;
    desktopState.token = String(info.sessionToken || "");
    if (!desktopState.token) throw new Error("Локальный мост не выдал токен сессии.");
    if (!silent) show("desktop-result", `Локальный мост подключён. Версия: ${desktopState.bridgeVersion}; протокол: ${protocol}.`);
    return true;
  } catch (error) {
    desktopState.bridge = false;
    desktopState.token = "";
    if (!silent) show("desktop-result", error.message || "Локальный мост не отвечает.");
    return false;
  } finally {
    renderEnvironment();
    if (!silent) toggle(false);
  }
};

// A failed startup probe is final for this cycle. The 30-second health poll or
// a user click can retry; the UI must not appear to spin through 12 identical failures.
autoConnectCompanion = async function() {
  if (desktopBusy || desktopState.bridge || !desktopState.isDesktop) return;
  await checkCompanion(true);
};
