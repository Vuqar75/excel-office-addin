import { report as formulaLibrary } from "./test-formula-library.mjs";
import { report as formulaExperience } from "./test-formula-experience.mjs";
import { report as navigation } from "./test-navigation.mjs";
import { report as safety } from "./test-safety-center.mjs";
import { report as tools } from "./test-plex-tools.mjs";
import { report as helpSettings } from "./test-help-settings.mjs";
import { report as hardening } from "./test-hardening.mjs";
import { report as protocolCompatibility } from "./test-protocol-compat.mjs";

const reports = { formulaLibrary, formulaExperience, navigation, safety, tools, helpSettings, hardening, protocolCompatibility };
console.log(`PASS: ${Object.keys(reports).length} regression suites`);
console.log(JSON.stringify(reports));
export { reports };
