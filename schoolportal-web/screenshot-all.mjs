import { chromium } from "playwright";
import { mkdirSync } from "fs";

const BASE = "http://localhost:3000";
const OUT  = "./screenshots";
mkdirSync(OUT, { recursive: true });

async function go(page, url, wait = 2000) {
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 30000 });
  await page.waitForTimeout(wait);
}

const browser = await chromium.launch({ headless: true });
const ctx     = await browser.newContext({ viewport: { width: 1440, height: 900 } });
const page    = await ctx.newPage();

// Login
await go(page, `${BASE}/login`, 1000);
await page.screenshot({ path: `${OUT}/01-login.png`, fullPage: true });
await page.fill('input[type="email"]', "admin@demo.schoolportal.com");
await page.fill('input[type="password"]', "Admin@123");
await Promise.all([
  page.waitForNavigation({ waitUntil: "domcontentloaded", timeout: 20000 }),
  page.click('button[type="submit"]'),
]);
await page.waitForTimeout(2500);

const PAGES = [
  ["02-dashboard",     "/dashboard"],
  ["03-users",         "/users"],
  ["04-classes",       "/classes"],
  ["05-assignments",   "/assignments"],
  ["06-quizzes",       "/quizzes"],
  ["07-gradebook",     "/gradebook"],
  ["08-attendance",    "/attendance"],
  ["09-calendar",      "/calendar"],
  ["10-messages",      "/messages"],
  ["11-announcements", "/announcements"],
  ["12-analytics",     "/analytics"],
  ["13-settings",      "/settings"],
  ["14-courses",       "/courses"],
];

for (const [name, path] of PAGES) {
  console.log(`${name}…`);
  await go(page, `${BASE}${path}`, 2000);
  await page.screenshot({ path: `${OUT}/${name}.png`, fullPage: true });
}

await browser.close();
console.log("Done.");
