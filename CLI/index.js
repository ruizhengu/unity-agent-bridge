#!/usr/bin/env node

const http = require('http');
const { execSync } = require('child_process');

const args = process.argv.slice(2);
if (args[0] === 'doctor') {
  runDoctor();
  process.exit(0);
}

if (args[0] !== 'check') {
  console.log('Usage: unity-agent-cli <command>');
  console.log('');
  console.log('Commands:');
  console.log('  check   - Check for compilation errors');
  console.log('  doctor  - Diagnose connection issues');
  process.exit(0);
}

function makeRequest(path) {
  return new Promise((resolve, reject) => {
    const req = http.get(`http://127.0.0.1:5142${path}`, (res) => {
      let data = '';
      res.on('data', (chunk) => data += chunk);
      res.on('end', () => {
        if (res.statusCode !== 200) {
          reject(new Error(`Received status code ${res.statusCode}`));
        } else {
          try {
            resolve(JSON.parse(data));
          } catch (e) {
            reject(new Error('Failed to parse JSON response'));
          }
        }
      });
    });
    req.on('error', reject);
    req.setTimeout(2000, () => {
      req.destroy(new Error('Timeout'));
    });
  });
}

const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

async function runCheck() {
  // ... existing check code ...
  try {
    // 1. Send Refresh request to force Unity to look for new files
    try {
      await makeRequest('/refresh');
    } catch (e) {
      console.error('Unity Editor is not open or the server is not running.');
      console.error('');
      console.error('Run "unity-agent-cli doctor" for diagnostics.');
      process.exit(1);
    }

    // 2. Poll briefly to see if compilation actually starts
    // (Unity takes a moment to switch EditorApplication.isCompiling to true)
    let isCompiling = false;
    for (let i = 0; i < 15; i++) {
      await delay(500);
      try {
        const status = await makeRequest('/ping');
        if (status.isCompiling) {
          isCompiling = true;
          break;
        }
      } catch (e) {
        // Connection dropped means domain reload started!
        isCompiling = true;
        break;
      }
    }

    // 3. If it started, poll until it finishes
    let retries = 0;
    while (isCompiling) {
      try {
        const status = await makeRequest('/ping');
        if (retries > 0) {
          // We just reconnected after a Domain Reload!
          // That means compilation was 100% successful and is now over.
          isCompiling = false;
        } else {
          isCompiling = status.isCompiling;
        }
        retries = 0; // reset retries on successful connection
      } catch (e) {
        // If the connection drops during polling, Unity is doing a Domain Reload!
        retries++;
        if (retries > 30) {
          console.error('Unity server disconnected during Domain Reload and hasn\'t returned after 15 seconds.');
          process.exit(1);
        }
      }

      if (isCompiling || retries > 0) {
        await delay(500);
      }
    }

    // Give Unity's script compilation a tiny bit more time to populate LogEntries
    await delay(1000);

    // 4. Fetch the final compile errors from Unity's LogEntries
    const errors = await makeRequest('/compile-errors');

    if (errors.length === 0) {
      console.log('✅ Compile Success');
      process.exit(0);
    } else {
      console.error('❌ Compilation Errors Found:');
      errors.forEach((e) => {
        console.error(`\nFile: ${e.File}:${e.Line}`);
        console.error(`Message: ${e.Message}`);
      });
      process.exit(1);
    }
  } catch (e) {
    console.error('Error during check:', e.message);
    process.exit(1);
  }
}

async function runDoctor() {
  console.log('🔍 Unity Agent Bridge Diagnostics');
  console.log('==================================\n');

  // Check 1: Is Unity running?
  console.log('1. Checking if Unity Editor is running...');
  let unityRunning = false;
  try {
    if (process.platform === 'darwin') {
      const result = execSync('pgrep -f "Unity"', { encoding: 'utf8' });
      unityRunning = result.trim().length > 0;
    } else if (process.platform === 'win32') {
      const result = execSync('tasklist | findstr Unity', { encoding: 'utf8' });
      unityRunning = result.trim().length > 0;
    }
  } catch (e) {
    // Unity not found
  }

  if (unityRunning) {
    console.log('   ✅ Unity Editor appears to be running');
  } else {
    console.log('   ❌ Unity Editor is not running');
    console.log('   → Please open Unity and try again');
    process.exit(1);
  }

  // Check 2: Can we connect to the server?
  console.log('\n2. Checking server connection...');
  console.log('   → Pinging http://127.0.0.1:5142/ping...');

  let serverResponding = false;
  let pingResult = null;
  const startTime = Date.now();

  try {
    pingResult = await makeRequest('/ping');
    serverResponding = true;
    const elapsed = Date.now() - startTime;
    console.log(`   ✅ Server responded in ${elapsed}ms`);
    console.log(`   → isCompiling: ${pingResult.isCompiling}`);
  } catch (e) {
    console.log('   ❌ Cannot connect to server');
    console.log(`   → Error: ${e.message}`);
  }

  // Check 3: Is the package installed?
  console.log('\n3. Checking package installation...');
  if (unityRunning && !serverResponding) {
    console.log('   ❌ Server not responding - package may not be installed');
    console.log('');
    console.log('   📦 Installation:');
    console.log('   1. Open Unity Editor');
    console.log('   2. Window > Package Manager > + > Add package from git URL');
    console.log('   3. Paste: https://github.com/ruizhengu/unity-agent-bridge.git?path=/UnityPackage');
    console.log('   4. Wait for compilation');
    console.log('');
    console.log('   🔍 After installation, check Unity Console for:');
    console.log('      "Unity Agent Server started on http://127.0.0.1:5142/"');
    console.log('');
    console.log('   Then run: unity-agent-cli doctor');
  } else if (!unityRunning) {
    console.log('   ⏳ Cannot check - Unity is not running');
  } else {
    console.log('   ✅ Package is installed and working correctly');
  }

  console.log('\n==================================');
}

runCheck();
