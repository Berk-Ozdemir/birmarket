import { defineConfig, devices } from '@playwright/test';
import os from 'node:os';
import path from 'node:path';

const projectDirectory = __dirname;
const repositoryRoot = path.resolve(projectDirectory, '../..');
const e2eDataDirectory = path.join(os.tmpdir(), `birmarket-e2e-${process.pid}-${Date.now()}`);

export default defineConfig({
  testDir: './playwright',
  testMatch: '**/*.e2e.ts',
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: 'http://127.0.0.1:4210',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile-chrome', use: { ...devices['Pixel 7'] } },
  ],
  webServer: [
    {
      command:
        'dotnet run --no-launch-profile --project backend/Birmarket.Api/Birmarket.Api.csproj',
      cwd: repositoryRoot,
      url: 'http://127.0.0.1:5145/api/health/ready',
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: 'http://127.0.0.1:5145',
        birmarket_demo_mode: 'true',
        birmarket_demo_sqlite_path: path.join(e2eDataDirectory, 'birmarket-demo.sqlite3'),
      },
      reuseExistingServer: false,
      timeout: 120_000,
    },
    {
      command: 'npm run e2e:web',
      cwd: projectDirectory,
      url: 'http://127.0.0.1:4210',
      reuseExistingServer: false,
      timeout: 120_000,
    },
  ],
});
