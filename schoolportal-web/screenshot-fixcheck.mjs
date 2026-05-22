import { chromium } from "playwright";
import { mkdirSync } from "fs";

const BASE  = "http://localhost:3000";
const OUT   = "./screenshots";
mkdirSync(OUT, { recursive: true });

async function go(page, url, wait = 2500) {
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 30000 });
  await page.waitForTimeout(wait);
}

const browser = await chromium.launch({ headless: true });
const ctx     = await browser.newContext({ viewport: { width: 1440, height: 900 } });
const page    = await ctx.newPage();

// Login
await go(page, `${BASE}/login`, 1000);
await page.fill('input[type="email"]',    "admin@demo.schoolportal.com");
await page.fill('input[type="password"]', "Admin@123");
await Promise.all([
  page.waitForNavigation({ waitUntil: "domcontentloaded", timeout: 20000 }),
  page.click('button[type="submit"]'),
]);
await page.waitForTimeout(2500);

// Gradebook (Admin — should show class selector matrix)
console.log("gradebook…");
await go(page, `${BASE}/gradebook`, 3000);
await page.screenshot({ path: `${OUT}/07-gradebook.png`, fullPage: true });

// Assignments (Admin — should show Create button + empty state)
console.log("assignments…");
await go(page, `${BASE}/assignments`, 2500);
await page.screenshot({ path: `${OUT}/05-assignments.png`, fullPage: true });

// Assignments modal open
console.log("assignments modal…");
const btn = page.locator("button", { hasText: "Create Assignment" }).first();
if (await btn.isVisible()) {
  await btn.click();
  await page.waitForTimeout(1500);
  await page.screenshot({ path: `${OUT}/05-assignments-modal.png`, fullPage: true });
}

await browser.close();
console.log("Done.");
