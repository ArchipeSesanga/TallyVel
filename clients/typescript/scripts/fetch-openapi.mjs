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
await writeFile(new URL("../openapi.json", import.meta.url), JSON.stringify(document, null, 2) + "\n");
console.log(`Wrote openapi.json from ${url}`);
