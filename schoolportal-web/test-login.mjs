import { chromium } from "playwright";

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

// Login
await page.goto("http://localhost:3000/login");
await page.fill('input[type="email"]', "admin@demo.schoolportal.com");
await page.fill('input[type="password"]', "Admin@123");
await Promise.all([
  page.waitForNavigation({ timeout: 15000 }).catch(() => {}),
  page.click('button[type="submit"]'),
]);
await page.waitForTimeout(2000);

// Screenshot each page
const routes = ["/dashboard", "/users", "/classes", "/attendance", "/announcements"];
for (const route of routes) {
  await page.goto(`http://localhost:3000${route}`);
  await page.waitForTimeout(2500);
  await page.screenshot({ path: `screenshot${route.replace("/", "-")}.png` });
  console.log(`Captured ${route}`);
}

await browser.close();
console.log("Done.");
