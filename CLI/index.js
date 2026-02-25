#!/usr/bin/env node

const http = require('http');

const args = process.argv.slice(2);
if (args[0] !== 'check') {
  console.log('Usage: unity-agent-cli check');
  process.exit(0);
}

const req = http.get('http://127.0.0.1:5142/compile-errors', (res) => {
  let data = '';

  res.on('data', (chunk) => {
    data += chunk;
  });

  res.on('end', () => {
    if (res.statusCode !== 200) {
      console.error(`Error: Received status code ${res.statusCode} from Unity Server.`);
      process.exit(1);
    }

    try {
      const errors = JSON.parse(data);
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
      console.error('Failed to parse response from Unity Server.', e);
      process.exit(1);
    }
  });
});

req.on('error', (e) => {
  if (e.code === 'ECONNREFUSED') {
    console.error('Unity Editor is not open or the server is not running.');
  } else {
    console.error('Error connecting to Unity Server:', e.message);
  }
  process.exit(1);
});
