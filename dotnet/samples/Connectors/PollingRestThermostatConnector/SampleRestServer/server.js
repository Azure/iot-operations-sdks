const fs = require('fs');
const basicAuth = require('basic-auth');
const express = require("express");
const app = express();
const port = 8000;

// Add request logging middleware BEFORE authentication
app.use((req, res, next) => {
    const timestamp = new Date().toISOString();
    const clientIP = req.ip || req.connection.remoteAddress || req.socket.remoteAddress;
    console.log(`[${timestamp}] === INCOMING REQUEST ===`);
    console.log(`[${timestamp}] Method: ${req.method}`);
    console.log(`[${timestamp}] URL: ${req.url}`);
    console.log(`[${timestamp}] Client IP: ${clientIP}`);
    console.log(`[${timestamp}] Headers: ${JSON.stringify(req.headers, null, 2)}`);
    console.log(`[${timestamp}] User-Agent: ${req.get('User-Agent') || 'Not provided'}`);
    console.log(`[${timestamp}] Authorization Header: ${req.get('Authorization') ? 'Present' : 'Missing'}`);
    next();
});

function readSecret(secretPath) {
    try {
        const secret = fs.readFileSync(secretPath, 'utf8').trim();
        console.log(`✅ Successfully read secret from ${secretPath}`);
        return secret;
    } catch (error) {
        console.error(`❌ Error reading secret from ${secretPath}:`, error.message);
        return null;
    }
}

// Read credentials from mounted secret volume
console.log('🔐 Reading authentication credentials...');
const USERNAME = readSecret('/var/secrets/testsecret/usernameKey');
const PASSWORD = readSecret('/var/secrets/testsecret/passwordKey');

if (!USERNAME || !PASSWORD) {
    console.error('❌ Authentication credentials not found in mounted secrets');
    console.error('Expected paths:');
    console.error('  - /var/secrets/testsecret/usernameKey');
    console.error('  - /var/secrets/testsecret/passwordKey');
    process.exit(1);
}

console.log('✅ Authentication configured successfully');
console.log(`📝 Username: ${USERNAME}`);
console.log(`📝 Password length: ${PASSWORD.length} characters`);

// In-memory store for users
const users = {
    [USERNAME]: PASSWORD
};

// Enhanced authentication middleware with detailed logging
const authenticate = (req, res, next) => {
    const timestamp = new Date().toISOString();
    console.log(`[${timestamp}] === AUTHENTICATION CHECK ===`);

    const authHeader = req.get('Authorization');
    if (!authHeader) {
        console.log(`[${timestamp}] ❌ No Authorization header found`);
        res.set('WWW-Authenticate', 'Basic realm="Factory Sensor API"');
        return res.status(401).json({
            error: 'Unauthorized',
            message: 'Authorization header required',
            timestamp: timestamp
        });
    }

    console.log(`[${timestamp}] 📋 Authorization header present: ${authHeader.substring(0, 20)}...`);

    const user = basicAuth(req);
    if (!user) {
        console.log(`[${timestamp}] ❌ Failed to parse Basic Auth credentials`);
        res.set('WWW-Authenticate', 'Basic realm="Factory Sensor API"');
        return res.status(401).json({
            error: 'Unauthorized',
            message: 'Invalid authorization format',
            timestamp: timestamp
        });
    }

    console.log(`[${timestamp}] 👤 Parsed username: "${user.name}"`);
    console.log(`[${timestamp}] 🔑 Password length: ${user.pass ? user.pass.length : 0} characters`);
    console.log(`[${timestamp}] 🔍 Expected username: "${USERNAME}"`);
    console.log(`[${timestamp}] 🔍 Expected password length: ${PASSWORD.length} characters`);

    if (user.name !== USERNAME) {
        console.log(`[${timestamp}] ❌ Username mismatch: got "${user.name}", expected "${USERNAME}"`);
        res.set('WWW-Authenticate', 'Basic realm="Factory Sensor API"');
        return res.status(401).json({
            error: 'Unauthorized',
            message: 'Invalid username',
            timestamp: timestamp
        });
    }

    if (user.pass !== PASSWORD) {
        console.log(`[${timestamp}] ❌ Password mismatch`);
        res.set('WWW-Authenticate', 'Basic realm="Factory Sensor API"');
        return res.status(401).json({
            error: 'Unauthorized',
            message: 'Invalid password',
            timestamp: timestamp
        });
    }

    console.log(`[${timestamp}] ✅ Authentication successful for user: ${user.name}`);
    next();
};

// Apply the authentication middleware to all routes
app.use(authenticate);

// Function to generate random temperature values in Fahrenheit
function getRandomTemperature(min, max) {
    return (Math.random() * (max - min) + min).toFixed(2);
}

// Function to generate random humidity values (percentage)
function getRandomHumidity(min, max) {
    return (Math.random() * (max - min) + min).toFixed(1);
}

// Function to get random floor number (1-10)
function getRandomFloor() {
    return Math.floor(Math.random() * 10) + 1;
}

// Function to get random factory ID
function getRandomFactoryId() {
    const factories = ['FAC001', 'FAC002', 'FAC003', 'FAC004', 'FAC005'];
    return factories[Math.floor(Math.random() * factories.length)];
}

// Initialize variables
let desiredTemperature = getRandomTemperature(68, 77);
let currentTemperature = getRandomTemperature(68, 77);
let thermostatPower = "on";

// API 1: Environmental Data (Temperature and Humidity)
app.get("/api/sensor/env", (req, res) => {
    const temperature = parseFloat(getRandomTemperature(68, 77));
    const humidity = parseFloat(getRandomHumidity(30, 70));

    res.json({
        temperature: temperature,
        humidity: humidity,

        unit: {
            temperature: "Fahrenheit",
            humidity: "Percentage"
        }
    });
});

// API 2: Location Data (Floor Number and Factory ID)
// app.get("/api/factory/location", (req, res) => {
//     const floorNumber = getRandomFloor();
//     const factoryId = getRandomFactoryId();

//     res.json({
//         floorNumber: floorNumber,
//         factoryId: factoryId,
//         timestamp: new Date().toISOString()
//     });
// });

app.get("/api/factory/location", (req, res) => {
    console.log(`[${new Date().toISOString()}] GET request received at /api/factory/location`);

    const floorNumber = getRandomFloor();
    const factoryId = getRandomFactoryId();

    console.log(`[${new Date().toISOString()}] Responding with floorNumber: ${floorNumber}, factoryId: ${factoryId}`);

    res.json({
        floorNumber: floorNumber,
        factoryId: factoryId,
        timestamp: new Date().toISOString()
    });
});

// Combined API: All data in one response (optional)
app.get("/api/sensor-data", (req, res) => {
    const temperature = parseFloat(getRandomTemperature(68, 77));
    const humidity = parseFloat(getRandomHumidity(30, 70));
    const floorNumber = getRandomFloor();
    const factoryId = getRandomFactoryId();

    res.json({
        environmental: {
            temperature: temperature,
            humidity: humidity
        },
        location: {
            floorNumber: floorNumber,
            factoryId: factoryId
        },
        timestamp: new Date().toISOString(),
        units: {
            temperature: "Fahrenheit",
            humidity: "Percentage"
        }
    });
});

app.listen(port, () => {
    console.log(`Enhanced Sensor API server running on port ${port}`);
    console.log(`Available endpoints:`);
    console.log(`  GET /api/sensor/env - Temperature and humidity data`);
    console.log(`  GET /api/factory/location - Floor number and factory ID`);
    console.log(`  GET /api/sensor-data - Combined environmental and location data`);
});