import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { crc32, createZip } from "../features/portfolio/lib/zip";

test("crc32 de un valor conocido", () => {
  assert.equal(crc32(Buffer.from("123456789")), 0xcbf43926);
});

test("el ZIP se puede abrir con unzip y conserva nombres con tildes y el contenido", { skip: !hasUnzip() }, () => {
  const dir = mkdtempSync(join(tmpdir(), "zip-"));
  const entries = [
    { name: "Portfolio_2_with_transaction_history.csv", data: Buffer.from("﻿a;b\r\n1;2\r\n") },
    { name: "Resumen ñ.csv", data: Buffer.from("x".repeat(5000)) },
  ];
  const file = join(dir, "t.zip");
  writeFileSync(file, createZip(entries));
  execFileSync("unzip", ["-tq", file]);
  const out = join(dir, "out");
  execFileSync("unzip", ["-q", file, "-d", out]);
  assert.equal(readFileSync(join(out, entries[0].name), "utf8"), "﻿a;b\r\n1;2\r\n");
  assert.equal(readFileSync(join(out, entries[1].name), "utf8").length, 5000);
});

function hasUnzip(): boolean {
  try {
    execFileSync("unzip", ["-v"], { stdio: "ignore" });
    return true;
  } catch {
    return false;
  }
}
