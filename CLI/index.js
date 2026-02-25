#!/usr/bin/env node

const http = require('http');

const args = process.argv.slice(2);
if (args[0] !== 'check') {
  console.log('Usage: unity-agent-cli check');
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
  try {
    // 1. Send Refresh request to force Unity to look for new files
    try {
      await makeRequest('/refresh');
    } catch (e) {
      console.error('Unity Editor is not open or the server is not running.');
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

runCheck();
