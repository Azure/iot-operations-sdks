// server.js
const basicAuth = require('basic-auth');
const express = require("express");
const app = express();

const port = 80;

const USERNAME = process.env.SERVICE_USERNAME;
const PASSWORD = process.env.SERVICE_PASSWORD;

// In-memory store for users (for demonstration purposes)
const users = {
    [USERNAME]: PASSWORD
};

// Minimal logging middleware - logs every API hit
app.use((req, res, next) => {
    console.log(`[${new Date().toISOString()}] ${req.method} ${req.url}`);
    next();
});

// Middleware to check Basic Authentication
const authenticate = (req, res, next) => {
    const user = basicAuth(req);
    if (user && users[user.name] === user.pass) {
        next();
    } else {
        res.set('WWW-Authenticate', 'Basic realm="example"');
        res.status(401).json({ error: 'Unauthorized' });
    }
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

let desiredTemperature = getRandomTemperature(68, 77);
let currentTemperature = getRandomTemperature(68, 77);
let thermostatPower = "on";

// Get Current Temperature
app.get("/api/thermostat/current", (req, res) => {
    currentTemperature = getRandomTemperature(68, 77);
    res.json({ currentTemperature: parseFloat(currentTemperature) });
});

// Get Desired Temperature
app.get("/api/thermostat/desired", (req, res) => {
    res.json({ desiredTemperature: parseFloat(desiredTemperature) });
});

// Set Desired Temperature
app.post("/api/thermostat/desired", express.json(), (req, res) => {
    if (req.body.desiredTemperature) {
        desiredTemperature = req.body.desiredTemperature;
        res.json({ message: "Desired temperature set successfully" });
    } else {
        res.status(400).json({ message: "Desired temperature is required" });
    }
});

// Get Thermostat Status
app.get("/api/thermostat/status", (req, res) => {
    currentTemperature = getRandomTemperature(68, 77);
    let status = desiredTemperature > currentTemperature ? "heating" : "cooling";
    res.json({
        status: status,
        currentTemperature: parseFloat(currentTemperature),
        desiredTemperature: parseFloat(desiredTemperature),
    });
});

// Toggle Thermostat Power
app.post("/api/thermostat/power", express.json(), (req, res) => {
    if (req.body.power === "on" || req.body.power === "off") {
        thermostatPower = req.body.power;
        res.json({ message: `Thermostat power turned ${thermostatPower}` });
    } else {
        res.status(400).json({ message: "Power state must be 'on' or 'off'" });
    }
});

// Factory Location API
app.get("/api/factory/location", (req, res) => {
    const floorNumber = getRandomFloor();
    const factoryId = getRandomFactoryId();

    res.json({
        floorNumber: floorNumber,
        factoryId: factoryId,
        timestamp: new Date().toISOString()
    });
});

// Environmental Sensor API
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

app.listen(port, () => {
    console.log(`REST API server running on port ${port}`);
    console.log('Available APIs:');
    console.log('  GET  /api/thermostat/current - Current temperature');
    console.log('  GET  /api/thermostat/desired - Desired temperature');
    console.log('  POST /api/thermostat/desired - Set desired temperature');
    console.log('  GET  /api/thermostat/status - Thermostat status');
    console.log('  POST /api/thermostat/power - Toggle thermostat power');
    console.log('  GET  /api/factory/location - Factory floor and ID');
    console.log('  GET  /api/sensor/env - Temperature and humidity');
});