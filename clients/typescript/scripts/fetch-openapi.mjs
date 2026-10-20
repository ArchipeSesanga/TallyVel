// Downloads the OpenAPI document from a running API into openapi.json.
// Usage: OPENAPI_URL=http://localhost:5203/openapi/v1.json npm run fetch
// (the default is the "http" launch profile; OpenAPI is only mapped in Development).
import { writeFile } from "node:fs/promises";

const url = process.env.OPENAPI_URL ?? "http://localhost:5203/openapi/v1.json";
const response = await fetch(url);
if (!response.ok) {
  console.error(`GET ${url} -> ${response.status}`);
  process.exit(1);
}
const document = await response.json();
// Descriptions come from XML doc comments, which carry the checkout's line endings (CRLF on
// Windows). Write LF, so the file is the same on every machine.
const text = JSON.stringify(document, null, 2).replaceAll("\\r\\n", "\\n");
await writeFile(new URL("../openapi.json", import.meta.url), text + "\n");
console.log(`Wrote openapi.json from ${url}`);
