// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::sync::LazyLock;

use super::UnitInfo;

pub(super) static UNIT_INFOS: LazyLock<HashMap<&'static str, UnitInfo>> = LazyLock::new(|| {
    HashMap::from([
        (
            // Ampere
            "A",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Hour
            "A-HR",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Hour per Cubic Decimetre
            "A-HR-PER-DeciM3",
            UnitInfo {
                kind: "ElectricChargeVolumeDensity",
                multiplier: 3600000.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Hour per Kilogram
            "A-HR-PER-KiloGM",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Hour per Square Metre
            "A-HR-PER-M2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Hour per Cubic Metre
            "A-HR-PER-M3",
            UnitInfo {
                kind: "ElectricChargeVolumeDensity",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Square Metre
            "A-M2",
            UnitInfo {
                kind: "MagneticAreaMoment",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Square Metre per Joule Second
            "A-M2-PER-J-SEC",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Minute
            "A-MIN",
            UnitInfo {
                kind: "BatteryCapacity",
                multiplier: 60.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Centimetre
            "A-PER-CentiM",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Square Centimetre
            "A-PER-CentiM2",
            UnitInfo {
                kind: "ElectricCurrentDensity",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Degree Celsius
            "A-PER-DEG_C",
            UnitInfo {
                kind: "ElectricCurrentPerTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Gram
            "A-PER-GM",
            UnitInfo {
                kind: "SpecificElectricCurrent",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Joule
            "A-PER-J",
            UnitInfo {
                kind: "ElectricCurrentPerEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Kelvin
            "A-PER-K",
            UnitInfo {
                kind: "ElectricCurrentPerTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Kilogram
            "A-PER-KiloGM",
            UnitInfo {
                kind: "MassicElectricCurrent",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Metre
            "A-PER-M",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Square Metre
            "A-PER-M2",
            UnitInfo {
                kind: "ElectricCurrentDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Square Metre Square Kelvin
            "A-PER-M2-K2",
            UnitInfo {
                kind: "RichardsonConstant",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Millimetre
            "A-PER-MilliM",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Square Millimetre
            "A-PER-MilliM2",
            UnitInfo {
                kind: "ElectricCurrentDensity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere per Radian
            "A-PER-RAD",
            UnitInfo {
                kind: "ElectricCurrentPerAngle",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Second
            "A-SEC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Acre
            "AC",
            UnitInfo {
                kind: "Area",
                multiplier: 4046.8564224,
                offset: 0.0,
            },
        ),
        (
            // Acre Foot
            "AC-FT",
            UnitInfo {
                kind: "Volume",
                multiplier: 1233.48183754752,
                offset: 0.0,
            },
        ),
        (
            // Acre Us Survey Foot
            "AC-FT_US",
            UnitInfo {
                kind: "Volume",
                multiplier: 1233.4842656613735,
                offset: 0.0,
            },
        ),
        (
            // Atomic Mass Unit
            "AMU",
            UnitInfo {
                kind: "Mass",
                multiplier: 1.66053878283e-27,
                offset: 0.0,
            },
        ),
        (
            // Angstrom
            "ANGSTROM",
            UnitInfo {
                kind: "Length",
                multiplier: 1e-10,
                offset: 0.0,
            },
        ),
        (
            // Cubic Angstrom
            "ANGSTROM3",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-30,
                offset: 0.0,
            },
        ),
        (
            // Arcminute
            "ARCMIN",
            UnitInfo {
                kind: "Angle",
                multiplier: 0.000290888209,
                offset: 0.0,
            },
        ),
        (
            // Arcsecond
            "ARCSEC",
            UnitInfo {
                kind: "Angle",
                multiplier: 4.84813681e-06,
                offset: 0.0,
            },
        ),
        (
            // Are
            "ARE",
            UnitInfo {
                kind: "Area",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Turn
            "AT",
            UnitInfo {
                kind: "MagnetomotiveForce",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ampere Turn per Inch
            "AT-PER-IN",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 39.37007874015748,
                offset: 0.0,
            },
        ),
        (
            // Ampere Turn per Metre
            "AT-PER-M",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Standard Atmosphere
            "ATM",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 101325.0,
                offset: 0.0,
            },
        ),
        (
            // Standard Atmosphere Cubic Metre per Mole
            "ATM-M3-PER-MOL",
            UnitInfo {
                kind: "HenrysLawVolatilityConstant",
                multiplier: 101325.0,
                offset: 0.0,
            },
        ),
        (
            // Standard Atmosphere per Metre
            "ATM-PER-M",
            UnitInfo {
                kind: "PressureGradient",
                multiplier: 101325.0,
                offset: 0.0,
            },
        ),
        (
            // Technical Atmosphere
            "ATM_T",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 98066.5,
                offset: 0.0,
            },
        ),
        (
            // Technical Atmosphere per Metre
            "ATM_T-PER-M",
            UnitInfo {
                kind: "PressureGradient",
                multiplier: 98066.5,
                offset: 0.0,
            },
        ),
        (
            // Astronomical-unit
            "AU",
            UnitInfo {
                kind: "Length",
                multiplier: 149597870691.6,
                offset: 0.0,
            },
        ),
        (
            // Abampere
            "A_Ab",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Abampere Square Centimetre
            "A_Ab-CentiM2",
            UnitInfo {
                kind: "MagneticAreaMoment",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Abampere per Square Centimetre
            "A_Ab-PER-CentiM2",
            UnitInfo {
                kind: "ElectricCurrentDensity",
                multiplier: 100000.0,
                offset: 0.0,
            },
        ),
        (
            // Statampere
            "A_Stat",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 3.335641e-10,
                offset: 0.0,
            },
        ),
        (
            // Statampere per Square Centimetre
            "A_Stat-PER-CentiM2",
            UnitInfo {
                kind: "ElectricCurrentDensity",
                multiplier: 3.335641e-06,
                offset: 0.0,
            },
        ),
        (
            // Attoampere
            "AttoA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Attocoulomb
            "AttoC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Attofarad
            "AttoFARAD",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Attojoule
            "AttoJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Attojoule Second
            "AttoJ-SEC",
            UnitInfo {
                kind: "Action",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Attosecond
            "AttoSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Bel
            "B",
            UnitInfo {
                kind: "SoundPressureLevel",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Bel per Metre
            "B-PER-M",
            UnitInfo {
                kind: "LinearLogarithmicRatio",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Ban
            "BAN",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 2.30258509,
                offset: 0.0,
            },
        ),
        (
            // Bar
            "BAR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 100000.0,
                offset: 0.0,
            },
        ),
        (
            // Bar Litre per Second
            "BAR-L-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Bar Cubic Metre per Second
            "BAR-M3-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 100000.0,
                offset: 0.0,
            },
        ),
        (
            // Bar per Bar
            "BAR-PER-BAR",
            UnitInfo {
                kind: "PressureRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Bar per Degree Celsius
            "BAR-PER-DEG_C",
            UnitInfo {
                kind: "PressureCoefficient",
                multiplier: 100000.0,
                offset: 0.0,
            },
        ),
        (
            // Bar per Kelvin
            "BAR-PER-K",
            UnitInfo {
                kind: "PressureCoefficient",
                multiplier: 100000.0,
                offset: 0.0,
            },
        ),
        (
            // Barad
            "BARAD",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Barn
            "BARN",
            UnitInfo {
                kind: "Area",
                multiplier: 1e-28,
                offset: 0.0,
            },
        ),
        (
            // Barye
            "BARYE",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Bar Absolute
            "BAR_A",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 100000.0,
                offset: 0.0,
            },
        ),
        (
            // Baud
            "BAUD",
            UnitInfo {
                kind: "DigitRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Barrel
            "BBL",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.1589873,
                offset: 0.0,
            },
        ),
        (
            // Barrel (UK Petroleum)
            "BBL_UK_PET",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.1591132,
                offset: 0.0,
            },
        ),
        (
            // Barrel (UK Petroleum) per Day
            "BBL_UK_PET-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.841587962962963e-06,
                offset: 0.0,
            },
        ),
        (
            // Barrel (UK Petroleum) per Hour
            "BBL_UK_PET-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 4.419811111111111e-05,
                offset: 0.0,
            },
        ),
        (
            // Barrel (UK Petroleum) per Minute
            "BBL_UK_PET-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0026518866666666667,
                offset: 0.0,
            },
        ),
        (
            // Barrel (UK Petroleum) per Second
            "BBL_UK_PET-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.1591132,
                offset: 0.0,
            },
        ),
        (
            // Barrel (US)
            "BBL_US",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 0.1589873,
                offset: 0.0,
            },
        ),
        (
            // Barrel (US) per Day
            "BBL_US-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.840130787037037e-06,
                offset: 0.0,
            },
        ),
        (
            // Barrel (US) per Minute
            "BBL_US-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0026497883333333333,
                offset: 0.0,
            },
        ),
        (
            // Dry Barrel (US)
            "BBL_US_DRY",
            UnitInfo {
                kind: "DryVolume",
                multiplier: 0.1156281989625,
                offset: 0.0,
            },
        ),
        (
            // Barrel (us Petroleum)
            "BBL_US_PET",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 0.1589873,
                offset: 0.0,
            },
        ),
        (
            // Barrel (us Petroleum) per Hour
            "BBL_US_PET-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 4.416313888888889e-05,
                offset: 0.0,
            },
        ),
        (
            // Barrel (us Petroleum) per Second
            "BBL_US_PET-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.1589873,
                offset: 0.0,
            },
        ),
        (
            // Beat
            "BEAT",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Beat per Minute
            "BEAT-PER-MIN",
            UnitInfo {
                kind: "HeartRate",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Beaufort
            "BFT",
            UnitInfo {
                kind: "Speed",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Billion (Long Scale)
            "BILLION_Long",
            UnitInfo {
                kind: "Count",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Billion (Short Scale)
            "BILLION_Short",
            UnitInfo {
                kind: "Count",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Biot
            "BIOT",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Bit
            "BIT",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 0.6931471805599453,
                offset: 0.0,
            },
        ),
        (
            // Bit per Metre
            "BIT-PER-M",
            UnitInfo {
                kind: "LinearBitDensity",
                multiplier: 0.6931471805599453,
                offset: 0.0,
            },
        ),
        (
            // Bit per Square Metre
            "BIT-PER-M2",
            UnitInfo {
                kind: "AreaBitDensity",
                multiplier: 0.6931471805599453,
                offset: 0.0,
            },
        ),
        (
            // Bit per Cubic Metre
            "BIT-PER-M3",
            UnitInfo {
                kind: "VolumetricBitDensity",
                multiplier: 0.6931471805599453,
                offset: 0.0,
            },
        ),
        (
            // Bit per Second
            "BIT-PER-SEC",
            UnitInfo {
                kind: "DataRate",
                multiplier: 0.6931471805599453,
                offset: 0.0,
            },
        ),
        (
            // Becquerel
            "BQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Becquerel per Kilogram
            "BQ-PER-KiloGM",
            UnitInfo {
                kind: "SpecificActivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Becquerel per Litre
            "BQ-PER-L",
            UnitInfo {
                kind: "ActivityConcentration",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Becquerel per Square Metre
            "BQ-PER-M2",
            UnitInfo {
                kind: "SurfaceActivityDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Becquerel per Cubic Metre
            "BQ-PER-M3",
            UnitInfo {
                kind: "ActivityConcentration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Becquerel Second per Cubic Metre
            "BQ-SEC-PER-M3",
            UnitInfo {
                kind: "AbsoluteActivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Breath
            "BREATH",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Breath per Minute
            "BREATH-PER-MIN",
            UnitInfo {
                kind: "RespiratoryRate",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Brewster
            "BREWSTER",
            UnitInfo {
                kind: "StressOpticCoefficient",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (39 °F)
            "BTU_39DEG_F",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 1059.67,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (59 °F)
            "BTU_59DEG_F",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 1054.8,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (60 °F)
            "BTU_60DEG_F",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 1054.68,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition)
            "BTU_IT",
            UnitInfo {
                kind: "Energy",
                multiplier: 1055.05585262,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) Foot
            "BTU_IT-FT",
            UnitInfo {
                kind: "ThermalEnergyLength",
                multiplier: 321.581023878576,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) Foot per Square Foot Hour Degree Fahrenheit
            "BTU_IT-FT-PER-FT2-HR-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 1.7307346663713912,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) Inch
            "BTU_IT-IN",
            UnitInfo {
                kind: "ThermalEnergyLength",
                multiplier: 26.798418656548,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) Inch per Square Foot Hour Degree Fahrenheit
            "BTU_IT-IN-PER-FT2-HR-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 0.1442278888642826,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) Inch per Square Foot Second Degree Fahrenheit
            "BTU_IT-IN-PER-FT2-SEC-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 519.2203999114173,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) Inch per Hour Square Foot Degree Fahrenheit
            "BTU_IT-IN-PER-HR-FT2-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 0.1442278888642826,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) Inch per Second Square Foot Degree Fahrenheit
            "BTU_IT-IN-PER-SEC-FT2-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 519.2203999114173,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Degree Fahrenheit
            "BTU_IT-PER-DEG_F",
            UnitInfo {
                kind: "HeatCapacity",
                multiplier: 1899.100534716,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Degree Rankine
            "BTU_IT-PER-DEG_R",
            UnitInfo {
                kind: "HeatCapacity",
                multiplier: 1899.1005347159999,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Square Foot
            "BTU_IT-PER-FT2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 11356.526682226975,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Square Foot Hour
            "BTU_IT-PER-FT2-HR",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 3.154590745063049,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Square Foot Hour Degree Fahrenheit
            "BTU_IT-PER-FT2-HR-DEG_F",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 5.678263341113488,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Square Foot Second
            "BTU_IT-PER-FT2-SEC",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 11356.526682226975,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Square Foot Second Degree Fahrenheit
            "BTU_IT-PER-FT2-SEC-DEG_F",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 20441.748028008555,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Cubic Foot
            "BTU_IT-PER-FT3",
            UnitInfo {
                kind: "EnergyDensity",
                multiplier: 37258.94580783128,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Hour
            "BTU_IT-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 0.2930710701722222,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Hour Square Foot
            "BTU_IT-PER-HR-FT2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 3.154590745063049,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Hour Square Foot Degree Fahrenheit
            "BTU_IT-PER-HR-FT2-DEG_F",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 5.678263341113488,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Hour Square Foot Degree Rankine
            "BTU_IT-PER-HR-FT2-DEG_R",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 5.678263341113487,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Square Inch Second
            "BTU_IT-PER-IN2-SEC",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 1635339.8422406844,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Pound Mass
            "BTU_IT-PER-LB",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 2326.0,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Pound Mass Degree Fahrenheit
            "BTU_IT-PER-LB-DEG_F",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 4186.8,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Pound Mass Degree Rankine
            "BTU_IT-PER-LB-DEG_R",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 4186.799999999999,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Pound Force
            "BTU_IT-PER-LB_F",
            UnitInfo {
                kind: "Length",
                multiplier: 237.18598049672096,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Pound Force Degree Fahrenheit
            "BTU_IT-PER-LB_F-DEG_F",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 426.9347648940977,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Pound Force Degree Rankine
            "BTU_IT-PER-LB_F-DEG_R",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 426.93476489409767,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Minute
            "BTU_IT-PER-MIN",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 17.584264210333334,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Pound Mole
            "BTU_IT-PER-MOL_LB",
            UnitInfo {
                kind: "EnergyPerMassAmountOfSubstance",
                multiplier: 2.326,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Pound Mole Degree Fahrenheit
            "BTU_IT-PER-MOL_LB-DEG_F",
            UnitInfo {
                kind: "MolarHeatCapacity",
                multiplier: 4.1868,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Second
            "BTU_IT-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1055.05585262,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Second Foot Degree Rankine
            "BTU_IT-PER-SEC-FT-DEG_R",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 6230.644798937007,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Second Square Foot
            "BTU_IT-PER-SEC-FT2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 11356.526682226975,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Second Square Foot Degree Fahrenheit
            "BTU_IT-PER-SEC-FT2-DEG_F",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 20441.748028008555,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (international Definition) per Second Square Foot Degree Rankine
            "BTU_IT-PER-SEC-FT2-DEG_R",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 20441.748028008555,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (mean)
            "BTU_MEAN",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 1055.05585262,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition)
            "BTU_TH",
            UnitInfo {
                kind: "Energy",
                multiplier: 1054.3502645,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) Foot per Square Foot Hour Degree Fahrenheit
            "BTU_TH-FT-PER-FT2-HR-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 1.7295772055446195,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) Foot per Hour Square Foot Degree Fahrenheit
            "BTU_TH-FT-PER-HR-FT2-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 1.7295772055446195,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) Inch per Square Foot Hour Degree Fahrenheit
            "BTU_TH-IN-PER-FT2-HR-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 0.14413143379538496,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) Inch per Square Foot Second Degree Fahrenheit
            "BTU_TH-IN-PER-FT2-SEC-DEG_F",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 518.8731616633859,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Degree Fahrenheit
            "BTU_TH-PER-DEG_F",
            UnitInfo {
                kind: "ThermalCapacitance",
                multiplier: 1897.8304761,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Degree Rankine
            "BTU_TH-PER-DEG_R",
            UnitInfo {
                kind: "ThermalCapacitance",
                multiplier: 1897.8304761,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Square Foot
            "BTU_TH-PER-FT2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 11348.931794912201,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Square Foot Hour
            "BTU_TH-PER-FT2-HR",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 3.152481054142278,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Square Foot Minute
            "BTU_TH-PER-FT2-MIN",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 189.14886324853668,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Square Foot Second
            "BTU_TH-PER-FT2-SEC",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 11348.931794912201,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Cubic Foot
            "BTU_TH-PER-FT3",
            UnitInfo {
                kind: "EnergyDensity",
                multiplier: 37234.02819853085,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Hour
            "BTU_TH-PER-HR",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 0.29287507347222225,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Hour Square Foot Degree Fahrenheit
            "BTU_TH-PER-HR-FT2-DEG_F",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 5.6744658974561,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Pound Mass
            "BTU_TH-PER-LB",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 2324.44444446894,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Pound Mass Degree Fahrenheit
            "BTU_TH-PER-LB-DEG_F",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 4184.000000044092,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Pound Mass Degree Rankine
            "BTU_TH-PER-LB-DEG_R",
            UnitInfo {
                kind: "MassicHeatCapacity",
                multiplier: 4184.000000044092,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Minute
            "BTU_TH-PER-MIN",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 17.572504408333334,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Second
            "BTU_TH-PER-SEC",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 1054.3502645,
                offset: 0.0,
            },
        ),
        (
            // British Thermal Unit (thermochemical Definition) per Second Square Foot Degree Fahrenheit
            "BTU_TH-PER-SEC-FT2-DEG_F",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 20428.077230841962,
                offset: 0.0,
            },
        ),
        (
            // Bushel (UK)
            "BU_UK",
            UnitInfo {
                kind: "DryVolume",
                multiplier: 0.03636872,
                offset: 0.0,
            },
        ),
        (
            // Bushel (UK) per Day
            "BU_UK-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 4.2093425925925925e-07,
                offset: 0.0,
            },
        ),
        (
            // Bushel (UK) per Hour
            "BU_UK-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.0102422222222223e-05,
                offset: 0.0,
            },
        ),
        (
            // Bushel (UK) per Minute
            "BU_UK-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0006061453333333334,
                offset: 0.0,
            },
        ),
        (
            // Bushel (UK) per Second
            "BU_UK-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.03636872,
                offset: 0.0,
            },
        ),
        (
            // Bushel (US)
            "BU_US",
            UnitInfo {
                kind: "DryVolume",
                multiplier: 0.03523907016688,
                offset: 0.0,
            },
        ),
        (
            // Bushel (US Dry)
            "BU_US_DRY",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.03523907,
                offset: 0.0,
            },
        ),
        (
            // Bushel (US Dry) per Day
            "BU_US_DRY-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 4.078596064814815e-07,
                offset: 0.0,
            },
        ),
        (
            // Bushel (US Dry) per Hour
            "BU_US_DRY-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 9.788630555555556e-06,
                offset: 0.0,
            },
        ),
        (
            // Bushel (US Dry) per Minute
            "BU_US_DRY-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0005873178333333333,
                offset: 0.0,
            },
        ),
        (
            // Bushel (US Dry) per Second
            "BU_US_DRY-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.03523907,
                offset: 0.0,
            },
        ),
        (
            // Byte
            "BYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5.545177444479562,
                offset: 0.0,
            },
        ),
        (
            // Byte per Second
            "BYTE-PER-SEC",
            UnitInfo {
                kind: "ByteRate",
                multiplier: 5.545177444479562,
                offset: 0.0,
            },
        ),
        (
            // Base Pair
            "BasePair",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb
            "C",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb Metre
            "C-M",
            UnitInfo {
                kind: "ElectricDipoleMoment",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb Square Metre
            "C-M2",
            UnitInfo {
                kind: "ElectricQuadrupoleMoment",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb Square Metre per Volt
            "C-M2-PER-V",
            UnitInfo {
                kind: "Polarizability",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Square Centimetre
            "C-PER-CentiM2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Cubic Centimetre
            "C-PER-CentiM3",
            UnitInfo {
                kind: "ElectricChargeVolumeDensity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Kilogram
            "C-PER-KiloGM",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Kilogram Second
            "C-PER-KiloGM-SEC",
            UnitInfo {
                kind: "ExposureRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Metre
            "C-PER-M",
            UnitInfo {
                kind: "ElectricChargeLineDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Square Metre
            "C-PER-M2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Cubic Metre
            "C-PER-M3",
            UnitInfo {
                kind: "ElectricChargeVolumeDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Mole
            "C-PER-MOL",
            UnitInfo {
                kind: "ElectricChargePerAmountOfSubstance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Square Millimetre
            "C-PER-MilliM2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Coulomb per Cubic Millimetre
            "C-PER-MilliM3",
            UnitInfo {
                kind: "ElectricChargeVolumeDensity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Square Coulomb Square Metre per Joule
            "C2-M2-PER-J",
            UnitInfo {
                kind: "Polarizability",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Coulomb Metre per Square Joule
            "C3-M-PER-J2",
            UnitInfo {
                kind: "CubicElectricDipoleMomentPerSquareEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Quartic Coulomb Quartic Metre per Cubic Joule
            "C4-M4-PER-J3",
            UnitInfo {
                kind: "QuarticElectricDipoleMomentPerCubicEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Calorie (15 Degrees C)
            "CAL_15DEG_C",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 4.1858,
                offset: 0.0,
            },
        ),
        (
            // Calorie (20 °C)
            "CAL_20DEG_C",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 4.1819,
                offset: 0.0,
            },
        ),
        (
            // International Table Calorie
            "CAL_IT",
            UnitInfo {
                kind: "Energy",
                multiplier: 4.1868,
                offset: 0.0,
            },
        ),
        (
            // International Table Calorie per Gram
            "CAL_IT-PER-GM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 4186.8,
                offset: 0.0,
            },
        ),
        (
            // International Table Calorie per Gram Degree Celsius
            "CAL_IT-PER-GM-DEG_C",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 4186.8,
                offset: 0.0,
            },
        ),
        (
            // International Table Calorie per Gram Kelvin
            "CAL_IT-PER-GM-K",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 4186.8,
                offset: 0.0,
            },
        ),
        (
            // International Table Calorie per Second Centimetre Kelvin
            "CAL_IT-PER-SEC-CentiM-K",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 418.68,
                offset: 0.0,
            },
        ),
        (
            // International Table Calorie per Second Square Centimetre Kelvin
            "CAL_IT-PER-SEC-CentiM2-K",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 41868.0,
                offset: 0.0,
            },
        ),
        (
            // Calorie (Mean)
            "CAL_MEAN",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 4.19,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie
            "CAL_TH",
            UnitInfo {
                kind: "Energy",
                multiplier: 4.184,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Centimetre Second Degree Celsius
            "CAL_TH-PER-CentiM-SEC-DEG_C",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 418.4,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Square Centimetre
            "CAL_TH-PER-CentiM2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 41840.0,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Square Centimetre Minute
            "CAL_TH-PER-CentiM2-MIN",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 697.3333333333334,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Square Centimetre Second
            "CAL_TH-PER-CentiM2-SEC",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 41840.0,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Cubic Centimetre Kelvin
            "CAL_TH-PER-CentiM3-K",
            UnitInfo {
                kind: "VolumetricHeatCapacity",
                multiplier: 4184000.0,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Gram
            "CAL_TH-PER-GM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Gram Degree Celsius
            "CAL_TH-PER-GM-DEG_C",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Gram Kelvin
            "CAL_TH-PER-GM-K",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Minute
            "CAL_TH-PER-MIN",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 0.06973333333333333,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Second
            "CAL_TH-PER-SEC",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 4.184,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Second Centimetre Kelvin
            "CAL_TH-PER-SEC-CentiM-K",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 418.4,
                offset: 0.0,
            },
        ),
        (
            // Thermochemical Calorie per Second Square Centimetre Kelvin
            "CAL_TH-PER-SEC-CentiM2-K",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 41840.0,
                offset: 0.0,
            },
        ),
        (
            // Carat
            "CARAT",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.0002,
                offset: 0.0,
            },
        ),
        (
            // Cases
            "CASES",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cases per Thousand Individuals Year
            "CASES-PER-KiloINDIV-YR",
            UnitInfo {
                kind: "MorbidityRate",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // United Arab Emirates Dirham
            "CCY_AED",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Afghani
            "CCY_AFN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lek
            "CCY_ALL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Armenian Dram
            "CCY_AMD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Netherlands Antillian Guilder
            "CCY_ANG",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kwanza
            "CCY_AOA",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Argentine Peso
            "CCY_ARS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Australian Dollar
            "CCY_AUD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Aruban Guilder
            "CCY_AWG",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Azerbaijanian Manat
            "CCY_AZN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Convertible Marks
            "CCY_BAM",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Barbados Dollar
            "CCY_BBD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Bangladeshi Taka
            "CCY_BDT",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Bulgarian Lev
            "CCY_BGN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Bulgarian Lev per Kilowatt Hour
            "CCY_BGN-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Bahraini Dinar
            "CCY_BHD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Burundian Franc
            "CCY_BIF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Bermuda Dollar
            "CCY_BMD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Brunei Dollar
            "CCY_BND",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Boliviano
            "CCY_BOB",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Bolivian Mvdol (funds Code)
            "CCY_BOV",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Brazilian Real
            "CCY_BRL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Bahamian Dollar
            "CCY_BSD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Bitcoin
            "CCY_BTC",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ngultrum
            "CCY_BTN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pula
            "CCY_BWP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Belarussian Ruble
            "CCY_BYN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Belize Dollar
            "CCY_BZD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Canadian Dollar
            "CCY_CAD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Franc Congolais
            "CCY_CDF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Wir Euro (complementary Currency)
            "CCY_CHE",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Swiss Franc
            "CCY_CHF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Swiss Franc per Hectare
            "CCY_CHF-PER-HA",
            UnitInfo {
                kind: "CostPerArea",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Swiss Franc per Kilogram
            "CCY_CHF-PER-KiloGM",
            UnitInfo {
                kind: "CostPerMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Swiss Franc per Kilowatt Hour
            "CCY_CHF-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Wir Franc (complementary Currency)
            "CCY_CHW",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Unidades De Formento (funds Code)
            "CCY_CLF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Chilean Peso
            "CCY_CLP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Yuan Renminbi
            "CCY_CNY",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Colombian Peso
            "CCY_COP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Unidad De Valor Real
            "CCY_COU",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Costa Rican Colon
            "CCY_CRC",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cuban Peso
            "CCY_CUP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cape Verde Escudo
            "CCY_CVE",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cyprus Pound
            "CCY_CYP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Czech Koruna
            "CCY_CZK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Czech Koruna per Kilowatt Hour
            "CCY_CZK-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Djibouti Franc
            "CCY_DJF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Danish Krone
            "CCY_DKK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Danish Krone per Kilowatt Hour
            "CCY_DKK-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Dominican Peso
            "CCY_DOP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Algerian Dinar
            "CCY_DZD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kroon
            "CCY_EEK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Egyptian Pound
            "CCY_EGP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Nakfa
            "CCY_ERN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ethiopian Birr
            "CCY_ETB",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ether
            "CCY_ETH",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Euro
            "CCY_EUR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Euro per Kilowatt
            "CCY_EUR-PER-KiloW",
            UnitInfo {
                kind: "CostPerPower",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Euro per Kilowatt Hour
            "CCY_EUR-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Euro per Square Metre
            "CCY_EUR-PER-M2",
            UnitInfo {
                kind: "CostPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Euro per Watt
            "CCY_EUR-PER-W",
            UnitInfo {
                kind: "CostPerPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Euro per Watt Hour
            "CCY_EUR-PER-W-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Euro per Watt Second
            "CCY_EUR-PER-W-SEC",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Fiji Dollar
            "CCY_FJD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Falkland Islands Pound
            "CCY_FKP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pound Sterling
            "CCY_GBP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pound Sterling per Kilowatt Hour
            "CCY_GBP-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Lari
            "CCY_GEL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cedi
            "CCY_GHS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gibraltar Pound
            "CCY_GIP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Dalasi
            "CCY_GMD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Guinea Franc
            "CCY_GNF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Quetzal
            "CCY_GTQ",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Guyana Dollar
            "CCY_GYD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Hong Kong Dollar
            "CCY_HKD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lempira
            "CCY_HNL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Croatian Kuna
            "CCY_HRK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Haiti Gourde
            "CCY_HTG",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Forint
            "CCY_HUF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Forint per Kilowatt Hour
            "CCY_HUF-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Rupiah
            "CCY_IDR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // New Israeli Shekel
            "CCY_ILS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Indian Rupee
            "CCY_INR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Iraqi Dinar
            "CCY_IQD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Iranian Rial
            "CCY_IRR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Iceland Krona
            "CCY_ISK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Jamaican Dollar
            "CCY_JMD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Jordanian Dinar
            "CCY_JOD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Japanese Yen
            "CCY_JPY",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kenyan Shilling
            "CCY_KES",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Som
            "CCY_KGS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Riel
            "CCY_KHR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Comoro Franc
            "CCY_KMF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // North Korean Won
            "CCY_KPW",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // South Korean Won
            "CCY_KRW",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kuwaiti Dinar
            "CCY_KWD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cayman Islands Dollar
            "CCY_KYD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Tenge
            "CCY_KZT",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lao Kip
            "CCY_LAK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lebanese Pound
            "CCY_LBP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Sri Lanka Rupee
            "CCY_LKR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Liberian Dollar
            "CCY_LRD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Loti
            "CCY_LSL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lithuanian Litas
            "CCY_LTL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Latvian Lats
            "CCY_LVL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Libyan Dinar
            "CCY_LYD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Moroccan Dirham
            "CCY_MAD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Moldovan Leu
            "CCY_MDL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Malagasy Ariary
            "CCY_MGA",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Denar
            "CCY_MKD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kyat
            "CCY_MMK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mongolian Tugrik
            "CCY_MNT",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pataca
            "CCY_MOP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ouguiya
            "CCY_MRU",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Maltese Lira
            "CCY_MTL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mauritius Rupee
            "CCY_MUR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Rufiyaa
            "CCY_MVR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Malawi Kwacha
            "CCY_MWK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mexican Peso
            "CCY_MXN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mexican Unidad De Inversion (udi) (funds Code)
            "CCY_MXV",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Malaysian Ringgit
            "CCY_MYR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metical
            "CCY_MZN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Namibian Dollar
            "CCY_NAD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Naira
            "CCY_NGN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cordoba Oro
            "CCY_NIO",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Norwegian Krone
            "CCY_NOK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Norwegian Krone per Kilowatt Hour
            "CCY_NOK-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Nepalese Rupee
            "CCY_NPR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // New Zealand Dollar
            "CCY_NZD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Rial Omani
            "CCY_OMR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Balboa
            "CCY_PAB",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Nuevo Sol
            "CCY_PEN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kina
            "CCY_PGK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Philippine Peso
            "CCY_PHP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pakistan Rupee
            "CCY_PKR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Zloty
            "CCY_PLN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Zloty per Kilowatt Hour
            "CCY_PLN-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Guarani
            "CCY_PYG",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Qatari Rial
            "CCY_QAR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Romanian New Leu
            "CCY_RON",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Romanian New Leu per Kilowatt Hour
            "CCY_RON-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Serbian Dinar
            "CCY_RSD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Russian Ruble
            "CCY_RUB",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Rwanda Franc
            "CCY_RWF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Saudi Riyal
            "CCY_SAR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Solomon Islands Dollar
            "CCY_SBD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Seychelles Rupee
            "CCY_SCR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Sudanese Pound
            "CCY_SDG",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Swedish Krona
            "CCY_SEK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Swedish Krona per Kilowatt Hour
            "CCY_SEK-PER-KiloW-HR",
            UnitInfo {
                kind: "CostPerEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Singapore Dollar
            "CCY_SGD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Saint Helena Pound
            "CCY_SHP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Slovak Koruna
            "CCY_SKK",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Leone
            "CCY_SLE",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Somali Shilling
            "CCY_SOS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Surinam Dollar
            "CCY_SRD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Dobra
            "CCY_STN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Syrian Pound
            "CCY_SYP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lilangeni
            "CCY_SZL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Baht
            "CCY_THB",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Somoni
            "CCY_TJS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Manat
            "CCY_TMT",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Tunisian Dinar
            "CCY_TND",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pa'anga
            "CCY_TOP",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // New Turkish Lira
            "CCY_TRY",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Trinidad and Tobago Dollar
            "CCY_TTD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // New Taiwan Dollar
            "CCY_TWD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Tanzanian Shilling
            "CCY_TZS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Hryvnia
            "CCY_UAH",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Uganda Shilling
            "CCY_UGX",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Us Dollar
            "CCY_USD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Tether USD
            "CCY_USDT",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // United States Dollar (next Day) (funds Code)
            "CCY_USN",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // United States Dollar (same Day) (funds Code)
            "CCY_USS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Peso Uruguayo
            "CCY_UYU",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Uzbekistan Som
            "CCY_UZS",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Venezuelan Bolvar
            "CCY_VES",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Vietnamese ??ng
            "CCY_VND",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Vatu
            "CCY_VUV",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Samoan Tala
            "CCY_WST",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cfa Franc Beac
            "CCY_XAF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Silver (one Troy Ounce)
            "CCY_XAG",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gold (one Troy Ounce)
            "CCY_XAU",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // European Composite Unit (eurco) (bonds Market Unit)
            "CCY_XBA",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // European Monetary Unit (e.m.u.-6) (bonds Market Unit)
            "CCY_XBB",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // European Unit of Account 9 (e.u.a.-9) (bonds Market Unit)
            "CCY_XBC",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // European Unit of Account 17 (e.u.a.-17) (bonds Market Unit)
            "CCY_XBD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // East Caribbean Dollar
            "CCY_XCD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Special Drawing Rights
            "CCY_XDR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gold Franc (special Settlement Currency)
            "CCY_XFO",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Uic Franc (special Settlement Currency)
            "CCY_XFU",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cfa Franc Bceao
            "CCY_XOF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Palladium (one Troy Ounce)
            "CCY_XPD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cfp Franc
            "CCY_XPF",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Platinum (one Troy Ounce)
            "CCY_XPT",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Yemeni Rial
            "CCY_YER",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // South African Rand
            "CCY_ZAR",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Zambian Kwacha
            "CCY_ZMW",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Zimbabwe Dollar
            "CCY_ZWL",
            UnitInfo {
                kind: "Currency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Candela
            "CD",
            UnitInfo {
                kind: "LuminousIntensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Candela per Square Foot
            "CD-PER-FT2",
            UnitInfo {
                kind: "Luminance",
                multiplier: 10.763910416709722,
                offset: 0.0,
            },
        ),
        (
            // Candela per Square Inch
            "CD-PER-IN2",
            UnitInfo {
                kind: "Luminance",
                multiplier: 1550.0031000062,
                offset: 0.0,
            },
        ),
        (
            // Candela per Kilolumen
            "CD-PER-KiloLM",
            UnitInfo {
                kind: "LuminousIntensityDistribution",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Candela per Lumen
            "CD-PER-LM",
            UnitInfo {
                kind: "LuminousIntensityDistribution",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Candela per Square Metre
            "CD-PER-M2",
            UnitInfo {
                kind: "Luminance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // International Candle
            "CD_IN",
            UnitInfo {
                kind: "LuminousIntensity",
                multiplier: 0.92,
                offset: 0.0,
            },
        ),
        (
            // Chain
            "CH",
            UnitInfo {
                kind: "Length",
                multiplier: 20.1168,
                offset: 0.0,
            },
        ),
        (
            // Chain (based on U.s. Survey Foot)
            "CHAIN_US",
            UnitInfo {
                kind: "Length",
                multiplier: 20.1168,
                offset: 0.0,
            },
        ),
        (
            // Curie
            "CI",
            UnitInfo {
                kind: "Activity",
                multiplier: 37000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Curie per Kilogram
            "CI-PER-KiloGM",
            UnitInfo {
                kind: "SpecificActivity",
                multiplier: 37000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Clo
            "CLO",
            UnitInfo {
                kind: "ThermalInsulance",
                multiplier: 0.155,
                offset: 0.0,
            },
        ),
        (
            // Cord
            "CORD",
            UnitInfo {
                kind: "DryVolume",
                multiplier: 3.62,
                offset: 0.0,
            },
        ),
        (
            // Count
            "COUNT",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Candlepower
            "CP",
            UnitInfo {
                kind: "LuminousIntensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Cup
            "CUP",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 0.00023658825,
                offset: 0.0,
            },
        ),
        (
            // Cup (US)
            "CUP_US",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 0.00023658825,
                offset: 0.0,
            },
        ),
        (
            // Long Hundred Weight
            "CWT_LONG",
            UnitInfo {
                kind: "Mass",
                multiplier: 50.80235,
                offset: 0.0,
            },
        ),
        (
            // Hundred Weight - Short
            "CWT_SHORT",
            UnitInfo {
                kind: "Mass",
                multiplier: 45.359237,
                offset: 0.0,
            },
        ),
        (
            // Cycle
            "CYC",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cycle per Second
            "CYC-PER-SEC",
            UnitInfo {
                kind: "RotationalFrequency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Abcoulomb
            "C_Ab",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Abcoulomb per Square Centimetre
            "C_Ab-PER-CentiM2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 100000.0,
                offset: 0.0,
            },
        ),
        (
            // Statcoulomb
            "C_Stat",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 3.3356409519815207e-10,
                offset: 0.0,
            },
        ),
        (
            // Statcoulomb per Square Centimetre
            "C_Stat-PER-CentiM2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 3.3356409519815205e-06,
                offset: 0.0,
            },
        ),
        (
            // Statcoulomb per Mole
            "C_Stat-PER-MOL",
            UnitInfo {
                kind: "ElectricChargePerAmountOfSubstance",
                multiplier: 3.3356409519815207e-10,
                offset: 0.0,
            },
        ),
        (
            // Centibar
            "CentiBAR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Centicoulomb
            "CentiC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centigram
            "CentiGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Centigray
            "CentiGRAY",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centilitre
            "CentiL",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Centimetre
            "CentiM",
            UnitInfo {
                kind: "Length",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centimetre per Hour
            "CentiM-PER-HR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 2.777777777777778e-06,
                offset: 0.0,
            },
        ),
        (
            // Centimetre per Kelvin
            "CentiM-PER-K",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centimetre per Kiloyear
            "CentiM-PER-KiloYR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 3.168808781402895e-13,
                offset: 0.0,
            },
        ),
        (
            // Centimetre per Second
            "CentiM-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centimetre per Square Second
            "CentiM-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centimetre per Year
            "CentiM-PER-YR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 3.168808781402895e-10,
                offset: 0.0,
            },
        ),
        (
            // Centimetre Second Degree Celsius
            "CentiM-SEC-DEG_C",
            UnitInfo {
                kind: "LengthTemperatureTime",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Square Centimetre
            "CentiM2",
            UnitInfo {
                kind: "Area",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Square Centimetre Minute
            "CentiM2-MIN",
            UnitInfo {
                kind: "AreaTime",
                multiplier: 0.006,
                offset: 0.0,
            },
        ),
        (
            // Square Centimetre per Erg
            "CentiM2-PER-ERG",
            UnitInfo {
                kind: "SpectralCrossSection",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Square Centimetre per Gram
            "CentiM2-PER-GM",
            UnitInfo {
                kind: "MassAttenuationCoefficient",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Square Centimetre per Second
            "CentiM2-PER-SEC",
            UnitInfo {
                kind: "AreaPerTime",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Square Centimetre per Steradian Erg
            "CentiM2-PER-SR-ERG",
            UnitInfo {
                kind: "SpectralAngularCrossSection",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Square Centimetre per Volt Second
            "CentiM2-PER-V-SEC",
            UnitInfo {
                kind: "Mobility",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Square Centimetre Second
            "CentiM2-SEC",
            UnitInfo {
                kind: "AreaTime",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre
            "CentiM3",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Cubic Centimetre
            "CentiM3-PER-CentiM3",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Day
            "CentiM3-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Gram
            "CentiM3-PER-GM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Hour
            "CentiM3-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Kelvin
            "CentiM3-PER-K",
            UnitInfo {
                kind: "VolumeThermalExpansion",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Cubic Metre
            "CentiM3-PER-M3",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Minute
            "CentiM3-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.6666666666666667e-08,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Mole
            "CentiM3-PER-MOL",
            UnitInfo {
                kind: "MolarRefractivity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Mole Second
            "CentiM3-PER-MOL-SEC",
            UnitInfo {
                kind: "AtmosphericHydroxylationRate",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Centimetre per Second
            "CentiM3-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Quartic Centimetre
            "CentiM4",
            UnitInfo {
                kind: "SecondAxialMomentOfArea",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Sextic Centimetre
            "CentiM6",
            UnitInfo {
                kind: "WarpingConstant",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Centimole
            "CentiMOL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centimole per Kilogram
            "CentiMOL-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centimole per Litre
            "CentiMOL-PER-L",
            UnitInfo {
                kind: "Concentration",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Conventional Centimetre of Water
            "CentiM_H2O",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 98.0665,
                offset: 0.0,
            },
        ),
        (
            // Centimetre of Water (4 °C)
            "CentiM_H2O_4DEG_C",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 98.0665,
                offset: 0.0,
            },
        ),
        (
            // Centimetre of Mercury
            "CentiM_HG",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1333.224,
                offset: 0.0,
            },
        ),
        (
            // Centimetre of Mercury (0 °C)
            "CentiM_HG_0DEG_C",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1333.224,
                offset: 0.0,
            },
        ),
        (
            // Centinewton
            "CentiN",
            UnitInfo {
                kind: "Force",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centinewton Metre
            "CentiN-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centinewton Metre per Square Metre
            "CentiN-M-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Centipoise
            "CentiPOISE",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Centipoise per Bar
            "CentiPOISE-PER-BAR",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Centistokes
            "CentiST",
            UnitInfo {
                kind: "KinematicViscosity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Dalton
            "DA",
            UnitInfo {
                kind: "MolecularMass",
                multiplier: 1.66053878283e-27,
                offset: 0.0,
            },
        ),
        (
            // Darcy
            "DARCY",
            UnitInfo {
                kind: "HydraulicPermeability",
                multiplier: 9.869233e-13,
                offset: 0.0,
            },
        ),
        (
            // Day
            "DAY",
            UnitInfo {
                kind: "Time",
                multiplier: 86400.0,
                offset: 0.0,
            },
        ),
        (
            // Day per Number
            "DAY-PER-NUM",
            UnitInfo {
                kind: "TimePerCount",
                multiplier: 86400.0,
                offset: 0.0,
            },
        ),
        (
            // Sidereal Day
            "DAY_Sidereal",
            UnitInfo {
                kind: "Time",
                multiplier: 86164.099,
                offset: 0.0,
            },
        ),
        (
            // Deaths
            "DEATHS",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Deaths per Thousand Individuals Year
            "DEATHS-PER-KiloINDIV-YR",
            UnitInfo {
                kind: "MortalityRate",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Deaths per Million Individuals Year
            "DEATHS-PER-MegaINDIV-YR",
            UnitInfo {
                kind: "MortalityRate",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Debye
            "DEBYE",
            UnitInfo {
                kind: "ElectricDipoleMoment",
                multiplier: 3.33564e-30,
                offset: 0.0,
            },
        ),
        (
            // Dec
            "DECADE",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree
            "DEG",
            UnitInfo {
                kind: "Angle",
                multiplier: 0.017453292519943295,
                offset: 0.0,
            },
        ),
        (
            // Degree per Hour
            "DEG-PER-HR",
            UnitInfo {
                kind: "AngularFrequency",
                multiplier: 4.84813681109536e-06,
                offset: 0.0,
            },
        ),
        (
            // Degree per Metre
            "DEG-PER-M",
            UnitInfo {
                kind: "AngularWavenumber",
                multiplier: 0.017453292519943295,
                offset: 0.0,
            },
        ),
        (
            // Degree per Minute
            "DEG-PER-MIN",
            UnitInfo {
                kind: "AngularFrequency",
                multiplier: 0.0002908882086657216,
                offset: 0.0,
            },
        ),
        (
            // Degree per Second
            "DEG-PER-SEC",
            UnitInfo {
                kind: "AngularFrequency",
                multiplier: 0.017453292519943295,
                offset: 0.0,
            },
        ),
        (
            // Degree per Square Second
            "DEG-PER-SEC2",
            UnitInfo {
                kind: "AngularAcceleration",
                multiplier: 0.017453292519943295,
                offset: 0.0,
            },
        ),
        (
            // Square Degree
            "DEG2",
            UnitInfo {
                kind: "SolidAngle",
                multiplier: 0.0003046174197867086,
                offset: 0.0,
            },
        ),
        (
            // Degree Api
            "DEGREE_API",
            UnitInfo {
                kind: "APIGravity",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Balling
            "DEGREE_BALLING",
            UnitInfo {
                kind: "Density",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Baume (origin Scale)
            "DEGREE_BAUME",
            UnitInfo {
                kind: "Density",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Baume (us Heavy)
            "DEGREE_BAUME_US_HEAVY",
            UnitInfo {
                kind: "Density",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Baume (us Light)
            "DEGREE_BAUME_US_LIGHT",
            UnitInfo {
                kind: "Density",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Brix
            "DEGREE_BRIX",
            UnitInfo {
                kind: "Density",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Oechsle
            "DEGREE_OECHSLE",
            UnitInfo {
                kind: "Density",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Plato
            "DEGREE_PLATO",
            UnitInfo {
                kind: "Density",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Twaddell
            "DEGREE_TWADDELL",
            UnitInfo {
                kind: "Density",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius
            "DEG_C",
            UnitInfo {
                kind: "Temperature",
                multiplier: 1.0,
                offset: 273.15,
            },
        ),
        (
            // Degree Celsius Centimetre
            "DEG_C-CentiM",
            UnitInfo {
                kind: "LengthTemperature",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius Day
            "DEG_C-DAY",
            UnitInfo {
                kind: "TimeTemperature",
                multiplier: 86400.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius Hour
            "DEG_C-HR",
            UnitInfo {
                kind: "TimeTemperature",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius per Hour
            "DEG_C-PER-HR",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius per Kelvin
            "DEG_C-PER-K",
            UnitInfo {
                kind: "TemperatureRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius per Metre
            "DEG_C-PER-M",
            UnitInfo {
                kind: "TemperatureGradient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius per Minute
            "DEG_C-PER-MIN",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius per Second
            "DEG_C-PER-SEC",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius per Year
            "DEG_C-PER-YR",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 3.168808781402895e-08,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius Week
            "DEG_C-WK",
            UnitInfo {
                kind: "TimeTemperature",
                multiplier: 604800.0,
                offset: 0.0,
            },
        ),
        (
            // Square Degree Celsius
            "DEG_C2",
            UnitInfo {
                kind: "TemperatureVariance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius Growing Cereal
            "DEG_C_GROWING_CEREAL",
            UnitInfo {
                kind: "Temperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Celsius Growing Cereal Day
            "DEG_C_GROWING_CEREAL-DAY",
            UnitInfo {
                kind: "GrowingDegreeDay",
                multiplier: 86400.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit
            "DEG_F",
            UnitInfo {
                kind: "Temperature",
                multiplier: 0.5555555555555556,
                offset: 459.67,
            },
        ),
        (
            // Degree Fahrenheit Day
            "DEG_F-DAY",
            UnitInfo {
                kind: "TimeTemperature",
                multiplier: 48000.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Hour
            "DEG_F-HR",
            UnitInfo {
                kind: "TimeTemperature",
                multiplier: 2000.0,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Hour Square Foot per British Thermal Unit (international Definition)
            "DEG_F-HR-FT2-PER-BTU_IT",
            UnitInfo {
                kind: "ThermalInsulance",
                multiplier: 0.17611018368230585,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Hour Square Foot per British Thermal Unit (international Definition) Inch
            "DEG_F-HR-FT2-PER-BTU_IT-IN",
            UnitInfo {
                kind: "ThermalResistivity",
                multiplier: 6.933471798515978,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Hour Square Foot per British Thermal Unit (thermochemical Definition)
            "DEG_F-HR-FT2-PER-BTU_TH",
            UnitInfo {
                kind: "ThermalInsulance",
                multiplier: 0.17622803944390722,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Hour Square Foot per British Thermal Unit (thermochemical Definition) Inch
            "DEG_F-HR-FT2-PER-BTU_TH-IN",
            UnitInfo {
                kind: "ThermalResistivity",
                multiplier: 6.938111789130205,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Hour per British Thermal Unit (international Definition)
            "DEG_F-HR-PER-BTU_IT",
            UnitInfo {
                kind: "ThermalResistance",
                multiplier: 1.8956342406266344,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Hour per British Thermal Unit (thermochemical Definition)
            "DEG_F-HR-PER-BTU_TH",
            UnitInfo {
                kind: "ThermalResistance",
                multiplier: 1.8969028294866046,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit per Hour
            "DEG_F-PER-HR",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.00015432098765432098,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit per Kelvin
            "DEG_F-PER-K",
            UnitInfo {
                kind: "TemperatureRatio",
                multiplier: 0.5555555555555556,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit per Minute
            "DEG_F-PER-MIN",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.009259259259259259,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit per Second
            "DEG_F-PER-SEC",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.5555555555555556,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit per Square Second
            "DEG_F-PER-SEC2",
            UnitInfo {
                kind: "TemperaturePerSquareTime",
                multiplier: 0.5555555555555556,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Second per British Thermal Unit (international Definition)
            "DEG_F-SEC-PER-BTU_IT",
            UnitInfo {
                kind: "ThermalResistance",
                multiplier: 0.0005265650668407317,
                offset: 0.0,
            },
        ),
        (
            // Degree Fahrenheit Second per British Thermal Unit (thermochemical Definition)
            "DEG_F-SEC-PER-BTU_TH",
            UnitInfo {
                kind: "ThermalResistance",
                multiplier: 0.000526917452635168,
                offset: 0.0,
            },
        ),
        (
            // Degree Rankine
            "DEG_R",
            UnitInfo {
                kind: "Temperature",
                multiplier: 0.5555555555555556,
                offset: 0.0,
            },
        ),
        (
            // Degree Rankine per Hour
            "DEG_R-PER-HR",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.000154320987654321,
                offset: 0.0,
            },
        ),
        (
            // Degree Rankine per Minute
            "DEG_R-PER-MIN",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.00925925925925926,
                offset: 0.0,
            },
        ),
        (
            // Degree Rankine per Second
            "DEG_R-PER-SEC",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.5555555555555556,
                offset: 0.0,
            },
        ),
        (
            // Denier
            "DENIER",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 1.1e-07,
                offset: 0.0,
            },
        ),
        (
            // Diopter
            "DIOPTER",
            UnitInfo {
                kind: "Curvature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Dots per Inch
            "DPI",
            UnitInfo {
                kind: "DotsPerInch",
                multiplier: 39.37007874015748,
                offset: 0.0,
            },
        ),
        (
            // Dram (UK)
            "DRAM_UK",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.0017718451953125,
                offset: 0.0,
            },
        ),
        (
            // Dram (US)
            "DRAM_US",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.0038879346,
                offset: 0.0,
            },
        ),
        (
            // Penny Weight
            "DWT",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.00155517384,
                offset: 0.0,
            },
        ),
        (
            // Dyne
            "DYN",
            UnitInfo {
                kind: "Force",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Dyne Centimetre
            "DYN-CentiM",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Dyne Metre
            "DYN-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Dyne per Centimetre
            "DYN-PER-CentiM",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Dyne per Square Centimetre
            "DYN-PER-CentiM2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Dyne Second per Centimetre
            "DYN-SEC-PER-CentiM",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Dyne Second per Cubic Centimetre
            "DYN-SEC-PER-CentiM3",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Dyne Second per Quintic Centimetre
            "DYN-SEC-PER-CentiM5",
            UnitInfo {
                kind: "PressureInRelationToVolumeFlowRate",
                multiplier: 100000.0,
                offset: 0.0,
            },
        ),
        (
            // Decaare
            "DecaARE",
            UnitInfo {
                kind: "Area",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Decacoulomb
            "DecaC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Decagram
            "DecaGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Decakelvin
            "DecaK",
            UnitInfo {
                kind: "Temperature",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Decalitre
            "DecaL",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Decametre
            "DecaM",
            UnitInfo {
                kind: "Length",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decametre
            "DecaM3",
            UnitInfo {
                kind: "Volume",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Decapascal
            "DecaPA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Decapoise
            "DecaPOISE",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Decibel
            "DeciB",
            UnitInfo {
                kind: "SoundPressureLevel",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Decibel per Kilometre
            "DeciB-PER-KiloM",
            UnitInfo {
                kind: "LinearLogarithmicRatio",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Decibel per Metre
            "DeciB-PER-M",
            UnitInfo {
                kind: "LinearLogarithmicRatio",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Decibar
            "DeciBAR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Decibar per Year
            "DeciBAR-PER-YR",
            UnitInfo {
                kind: "ForcePerAreaTime",
                multiplier: 0.0003168808781402895,
                offset: 0.0,
            },
        ),
        (
            // Decibel with A-weighting
            "DeciB_A",
            UnitInfo {
                kind: "SoundPressureLevel",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Decibel Carrier Unit
            "DeciB_C",
            UnitInfo {
                kind: "SignalDetectionThreshold",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Decibel Isotropic
            "DeciB_ISO",
            UnitInfo {
                kind: "SoundPressureLevel",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Decibel Referred to 1mw
            "DeciB_M",
            UnitInfo {
                kind: "SoundPressureLevel",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Decibel with Z-weighting
            "DeciB_Z",
            UnitInfo {
                kind: "SoundPressureLevel",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Decicoulomb
            "DeciC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Decigram
            "DeciGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Decilitre
            "DeciL",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Decilitre per Gram
            "DeciL-PER-GM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Decimetre
            "DeciM",
            UnitInfo {
                kind: "Length",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Square Decimetre
            "DeciM2",
            UnitInfo {
                kind: "Area",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decimetre
            "DeciM3",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decimetre per Day
            "DeciM3-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decimetre per Hour
            "DeciM3-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decimetre per Kilogram
            "DeciM3-PER-KiloGM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decimetre per Cubic Metre
            "DeciM3-PER-M3",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decimetre per Minute
            "DeciM3-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.6666666666666667e-05,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decimetre per Mole
            "DeciM3-PER-MOL",
            UnitInfo {
                kind: "MolarRefractivity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Cubic Decimetre per Second
            "DeciM3-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Decinewton
            "DeciN",
            UnitInfo {
                kind: "Force",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Decinewton Metre
            "DeciN-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Decisiemens
            "DeciS",
            UnitInfo {
                kind: "Admittance",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Decisiemens per Metre
            "DeciS-PER-M",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Decisecond
            "DeciSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Decitonne
            "DeciTONNE",
            UnitInfo {
                kind: "Mass",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Deci Metric Ton
            "DeciTON_Metric",
            UnitInfo {
                kind: "Mass",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Elementary Charge
            "E",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Enzyme Unit
            "ENZ",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 1.6667e-08,
                offset: 0.0,
            },
        ),
        (
            // Enzyme Unit per Litre
            "ENZ-PER-L",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1.6667e-05,
                offset: 0.0,
            },
        ),
        (
            // Equivalent
            "EQ",
            UnitInfo {
                kind: "ReactiveCharge",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Erg
            "ERG",
            UnitInfo {
                kind: "Energy",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Erg Square Centimetre
            "ERG-CentiM2",
            UnitInfo {
                kind: "MassStoppingPower",
                multiplier: 1e-11,
                offset: 0.0,
            },
        ),
        (
            // Erg Square Centimetre per Gram
            "ERG-CentiM2-PER-GM",
            UnitInfo {
                kind: "TotalMassStoppingPower",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Erg per Centimetre
            "ERG-PER-CentiM",
            UnitInfo {
                kind: "TotalLinearStoppingPower",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Erg per Square Centimetre
            "ERG-PER-CentiM2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Erg per Square Centimetre Second
            "ERG-PER-CentiM2-SEC",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Erg per Cubic Centimetre
            "ERG-PER-CentiM3",
            UnitInfo {
                kind: "EnergyDensity",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Erg per Gram
            "ERG-PER-GM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Erg per Gram Second
            "ERG-PER-GM-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Erg per Second
            "ERG-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Erg Second
            "ERG-SEC",
            UnitInfo {
                kind: "AngularImpulse",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Erlang
            "ERLANG",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Electron Volt
            "EV",
            UnitInfo {
                kind: "Energy",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Electron Volt Square Metre
            "EV-M2",
            UnitInfo {
                kind: "TotalAtomicStoppingPower",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Electron Volt Square Metre per Kilogram
            "EV-M2-PER-KiloGM",
            UnitInfo {
                kind: "TotalMassStoppingPower",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Electron Volt per Angstrom
            "EV-PER-ANGSTROM",
            UnitInfo {
                kind: "TotalLinearStoppingPower",
                multiplier: 1.602176634e-09,
                offset: 0.0,
            },
        ),
        (
            // Electron Volt per Kelvin
            "EV-PER-K",
            UnitInfo {
                kind: "HeatCapacity",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Electron Volt per Metre
            "EV-PER-M",
            UnitInfo {
                kind: "TotalLinearStoppingPower",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Electron Volt per Tesla
            "EV-PER-T",
            UnitInfo {
                kind: "MagneticAreaMoment",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Electron Volt Second
            "EV-SEC",
            UnitInfo {
                kind: "AngularImpulse",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Hartree
            "E_h",
            UnitInfo {
                kind: "Energy",
                multiplier: 4.35974394e-18,
                offset: 0.0,
            },
        ),
        (
            // Earth Mass
            "EarthMass",
            UnitInfo {
                kind: "Mass",
                multiplier: 5.97219e+24,
                offset: 0.0,
            },
        ),
        (
            // Elementary Charge
            "ElementaryCharge",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1.602176634e-19,
                offset: 0.0,
            },
        ),
        (
            // Exabit
            "ExaBIT",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 6.931471805599453e+17,
                offset: 0.0,
            },
        ),
        (
            // Exabit per Second
            "ExaBIT-PER-SEC",
            UnitInfo {
                kind: "DataRate",
                multiplier: 6.931471805599453e+17,
                offset: 0.0,
            },
        ),
        (
            // Exabyte
            "ExaBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5.545177444479563e+18,
                offset: 0.0,
            },
        ),
        (
            // Exacoulomb
            "ExaC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e+18,
                offset: 0.0,
            },
        ),
        (
            // Exajoule
            "ExaJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1e+18,
                offset: 0.0,
            },
        ),
        (
            // Exajoule per Second
            "ExaJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1e+18,
                offset: 0.0,
            },
        ),
        (
            // Exavolt
            "ExaV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1e+18,
                offset: 0.0,
            },
        ),
        (
            // Exa Volt Ampere
            "ExaVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1e+18,
                offset: 0.0,
            },
        ),
        (
            // Exawatt
            "ExaW",
            UnitInfo {
                kind: "Power",
                multiplier: 1e+18,
                offset: 0.0,
            },
        ),
        (
            // Exbibit
            "ExbiBIT",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 1.0599710976522067e+17,
                offset: 0.0,
            },
        ),
        (
            // Exbibit per Metre
            "ExbiBIT-PER-M",
            UnitInfo {
                kind: "LinearBitDensity",
                multiplier: 1.0599710976522067e+17,
                offset: 0.0,
            },
        ),
        (
            // Exbibit per Square Metre
            "ExbiBIT-PER-M2",
            UnitInfo {
                kind: "AreaBitDensity",
                multiplier: 1.0599710976522067e+17,
                offset: 0.0,
            },
        ),
        (
            // Exbibit per Cubic Metre
            "ExbiBIT-PER-M3",
            UnitInfo {
                kind: "VolumetricBitDensity",
                multiplier: 1.0599710976522067e+17,
                offset: 0.0,
            },
        ),
        (
            // Exbibyte
            "ExbiBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 8.479768781217654e+17,
                offset: 0.0,
            },
        ),
        (
            // Faraday
            "F",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 96485.3399,
                offset: 0.0,
            },
        ),
        (
            // Fractional Area
            "FA",
            UnitInfo {
                kind: "SolidAngle",
                multiplier: 12.5663706,
                offset: 0.0,
            },
        ),
        (
            // Farad
            "FARAD",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Farad per Kilometre
            "FARAD-PER-KiloM",
            UnitInfo {
                kind: "Permittivity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Farad per Metre
            "FARAD-PER-M",
            UnitInfo {
                kind: "Permittivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Abfarad
            "FARAD_Ab",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Abfarad per Centimetre
            "FARAD_Ab-PER-CentiM",
            UnitInfo {
                kind: "Permittivity",
                multiplier: 100000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Statfarad
            "FARAD_Stat",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1.1126500560536185e-18,
                offset: 0.0,
            },
        ),
        (
            // Fathom
            "FATH",
            UnitInfo {
                kind: "Length",
                multiplier: 1.8288,
                offset: 0.0,
            },
        ),
        (
            // Board Foot
            "FBM",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.00236,
                offset: 0.0,
            },
        ),
        (
            // Foot Candle
            "FC",
            UnitInfo {
                kind: "LuminousFluxPerArea",
                multiplier: 10.764,
                offset: 0.0,
            },
        ),
        (
            // Flight
            "FLIGHT",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Floating Point Operations per Second
            "FLOPS",
            UnitInfo {
                kind: "FloatingPointCalculationCapability",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Fermi
            "FM",
            UnitInfo {
                kind: "Length",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Franklin
            "FR",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 3.3356409519815207e-10,
                offset: 0.0,
            },
        ),
        (
            // Fraction
            "FRACTION",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Frame
            "FRAME",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Frame per Second
            "FRAME-PER-SEC",
            UnitInfo {
                kind: "VideoFrameRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Foot
            "FT",
            UnitInfo {
                kind: "Length",
                multiplier: 0.3048,
                offset: 0.0,
            },
        ),
        (
            // Foot Pound Force
            "FT-LB_F",
            UnitInfo {
                kind: "Energy",
                multiplier: 1.3558180091635803,
                offset: 0.0,
            },
        ),
        (
            // Foot Pound Force per Square Foot
            "FT-LB_F-PER-FT2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 14.5939035919985,
                offset: 0.0,
            },
        ),
        (
            // Foot Pound Force per Square Foot Second
            "FT-LB_F-PER-FT2-SEC",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 14.5939035919985,
                offset: 0.0,
            },
        ),
        (
            // Foot Pound Force per Hour
            "FT-LB_F-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 0.0003766161136565501,
                offset: 0.0,
            },
        ),
        (
            // Foot Pound Force per Square Metre
            "FT-LB_F-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1.3558180091635803,
                offset: 0.0,
            },
        ),
        (
            // Foot Pound Force per Minute
            "FT-LB_F-PER-MIN",
            UnitInfo {
                kind: "Power",
                multiplier: 0.022596966819393004,
                offset: 0.0,
            },
        ),
        (
            // Foot Pound Force per Second
            "FT-LB_F-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1.3558180091635803,
                offset: 0.0,
            },
        ),
        (
            // Foot Pound Force Second
            "FT-LB_F-SEC",
            UnitInfo {
                kind: "AngularImpulse",
                multiplier: 1.3558180091635803,
                offset: 0.0,
            },
        ),
        (
            // Foot Poundal
            "FT-PDL",
            UnitInfo {
                kind: "Energy",
                multiplier: 0.0421401100938048,
                offset: 0.0,
            },
        ),
        (
            // Foot per Day
            "FT-PER-DAY",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 3.527777777777778e-06,
                offset: 0.0,
            },
        ),
        (
            // Foot per Degree Fahrenheit
            "FT-PER-DEG_F",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 0.54864,
                offset: 0.0,
            },
        ),
        (
            // Foot per Hour
            "FT-PER-HR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 8.466666666666666e-05,
                offset: 0.0,
            },
        ),
        (
            // Foot per Minute
            "FT-PER-MIN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.00508,
                offset: 0.0,
            },
        ),
        (
            // Foot per Second
            "FT-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.3048,
                offset: 0.0,
            },
        ),
        (
            // Foot per Square Second
            "FT-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 0.3048,
                offset: 0.0,
            },
        ),
        (
            // Square Foot
            "FT2",
            UnitInfo {
                kind: "Area",
                multiplier: 0.09290304,
                offset: 0.0,
            },
        ),
        (
            // Square Foot Degree Fahrenheit
            "FT2-DEG_F",
            UnitInfo {
                kind: "AreaTemperature",
                multiplier: 0.0516128,
                offset: 0.0,
            },
        ),
        (
            // Square Foot Hour Degree Fahrenheit
            "FT2-HR-DEG_F",
            UnitInfo {
                kind: "AreaTimeTemperature",
                multiplier: 185.80608,
                offset: 0.0,
            },
        ),
        (
            // Square Foot Hour Degree Fahrenheit per British Thermal Unit (international Definition)
            "FT2-HR-DEG_F-PER-BTU_IT",
            UnitInfo {
                kind: "ThermalInsulance",
                multiplier: 0.17611018368230585,
                offset: 0.0,
            },
        ),
        (
            // Square Foot per Hour
            "FT2-PER-HR",
            UnitInfo {
                kind: "AreaPerTime",
                multiplier: 2.58064e-05,
                offset: 0.0,
            },
        ),
        (
            // Square Foot per Second
            "FT2-PER-SEC",
            UnitInfo {
                kind: "AreaPerTime",
                multiplier: 0.09290304,
                offset: 0.0,
            },
        ),
        (
            // Square Foot Second Degree Fahrenheit
            "FT2-SEC-DEG_F",
            UnitInfo {
                kind: "AreaTimeTemperature",
                multiplier: 0.0516128,
                offset: 0.0,
            },
        ),
        (
            // Cubic Foot
            "FT3",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.028316846592,
                offset: 0.0,
            },
        ),
        (
            // Cubic Foot per Day
            "FT3-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 3.2774128e-07,
                offset: 0.0,
            },
        ),
        (
            // Cubic Foot per Degree Fahrenheit
            "FT3-PER-DEG_F",
            UnitInfo {
                kind: "VolumeThermalExpansion",
                multiplier: 0.0509703238656,
                offset: 0.0,
            },
        ),
        (
            // Cubic Foot per Hour
            "FT3-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 7.86579072e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Foot per Pound Mass
            "FT3-PER-LB",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 0.06242796057614461,
                offset: 0.0,
            },
        ),
        (
            // Cubic Foot per Minute
            "FT3-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0004719474432,
                offset: 0.0,
            },
        ),
        (
            // Cubic Foot per Minute Square Foot
            "FT3-PER-MIN-FT2",
            UnitInfo {
                kind: "Speed",
                multiplier: 0.00508,
                offset: 0.0,
            },
        ),
        (
            // Cubic Foot per Second
            "FT3-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.028316846592,
                offset: 0.0,
            },
        ),
        (
            // Quartic Foot
            "FT4",
            UnitInfo {
                kind: "SecondAxialMomentOfArea",
                multiplier: 0.0086309748412416,
                offset: 0.0,
            },
        ),
        (
            // Foot of Water
            "FT_H2O",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 2989.067,
                offset: 0.0,
            },
        ),
        (
            // Foot of Water (39.2 °F)
            "FT_H2O_39dot2DEG_F",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 2989.067,
                offset: 0.0,
            },
        ),
        (
            // Foot of Mercury
            "FT_HG",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 40636.66,
                offset: 0.0,
            },
        ),
        (
            // Us Survey Foot
            "FT_US",
            UnitInfo {
                kind: "Length",
                multiplier: 0.3048006,
                offset: 0.0,
            },
        ),
        (
            // Furlong
            "FUR",
            UnitInfo {
                kind: "Length",
                multiplier: 201.168,
                offset: 0.0,
            },
        ),
        (
            // Femtoampere
            "FemtoA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtocoulomb
            "FemtoC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtofarad
            "FemtoFARAD",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtogram
            "FemtoGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Femtogram per Kilogram
            "FemtoGM-PER-KiloGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Femtogram per Litre
            "FemtoGM-PER-L",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtojoule
            "FemtoJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtolitre
            "FemtoL",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Femtometre
            "FemtoM",
            UnitInfo {
                kind: "Length",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtomole
            "FemtoMOL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtomole per Kilogram
            "FemtoMOL-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtomole per Litre
            "FemtoMOL-PER-L",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Femtosecond
            "FemtoSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Femtovolt
            "FemtoV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Gravity
            "G",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 9.80665,
                offset: 0.0,
            },
        ),
        (
            // Galileo
            "GALILEO",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Imperial Gallon
            "GAL_IMP",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 0.00454609,
                offset: 0.0,
            },
        ),
        (
            // Gallon (UK)
            "GAL_UK",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 0.00454609,
                offset: 0.0,
            },
        ),
        (
            // Gallon (UK) per Day
            "GAL_UK-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 5.2616782407407406e-08,
                offset: 0.0,
            },
        ),
        (
            // Gallon (UK) per Hour
            "GAL_UK-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.2628027777777779e-06,
                offset: 0.0,
            },
        ),
        (
            // Gallon (UK) per Minute
            "GAL_UK-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 7.576816666666667e-05,
                offset: 0.0,
            },
        ),
        (
            // Gallon (UK) per Second
            "GAL_UK-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.00454609,
                offset: 0.0,
            },
        ),
        (
            // Us Gallon
            "GAL_US",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 0.003785411784,
                offset: 0.0,
            },
        ),
        (
            // Us Gallon per Day
            "GAL_US-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 4.3812636388888886e-08,
                offset: 0.0,
            },
        ),
        (
            // Us Gallon per Hour
            "GAL_US-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.0515032733333334e-06,
                offset: 0.0,
            },
        ),
        (
            // Us Gallon per Minute
            "GAL_US-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 6.30901964e-05,
                offset: 0.0,
            },
        ),
        (
            // Us Gallon per Second
            "GAL_US-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.003785411784,
                offset: 0.0,
            },
        ),
        (
            // Dry Gallon Us
            "GAL_US_DRY",
            UnitInfo {
                kind: "DryVolume",
                multiplier: 0.00440488377086,
                offset: 0.0,
            },
        ),
        (
            // Gamma
            "GAMMA",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // French Gauge
            "GAUGE_FR",
            UnitInfo {
                kind: "Length",
                multiplier: 0.0003333333,
                offset: 0.0,
            },
        ),
        (
            // Gauss
            "GAUSS",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Charrière Gauge
            "GA_Charriere",
            UnitInfo {
                kind: "Length",
                multiplier: 0.0003333333,
                offset: 0.0,
            },
        ),
        (
            // Gilbert
            "GI",
            UnitInfo {
                kind: "MagnetomotiveForce",
                multiplier: 0.795774715,
                offset: 0.0,
            },
        ),
        (
            // Gill (UK)
            "GI_UK",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.0001420653,
                offset: 0.0,
            },
        ),
        (
            // Gill (UK) per Day
            "GI_UK-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.6442743055555556e-09,
                offset: 0.0,
            },
        ),
        (
            // Gill (UK) per Hour
            "GI_UK-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 3.9462583333333336e-08,
                offset: 0.0,
            },
        ),
        (
            // Gill (UK) per Minute
            "GI_UK-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.367755e-06,
                offset: 0.0,
            },
        ),
        (
            // Gill (UK) per Second
            "GI_UK-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0001420653,
                offset: 0.0,
            },
        ),
        (
            // Gill (US)
            "GI_US",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.000118294125,
                offset: 0.0,
            },
        ),
        (
            // Gill (US) per Day
            "GI_US-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.3691449652777778e-09,
                offset: 0.0,
            },
        ),
        (
            // Gill (US) per Hour
            "GI_US-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 3.285947916666667e-08,
                offset: 0.0,
            },
        ),
        (
            // Gill (US) per Minute
            "GI_US-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.97156875e-06,
                offset: 0.0,
            },
        ),
        (
            // Gill (US) per Second
            "GI_US-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.000118294125,
                offset: 0.0,
            },
        ),
        (
            // Gram
            "GM",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram Centimetre per Second
            "GM-CentiM-PER-SEC",
            UnitInfo {
                kind: "Impulse",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Gram Millimetre
            "GM-MilliM",
            UnitInfo {
                kind: "LengthMass",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Gram per Centimetre Bar
            "GM-PER-CentiM-BAR",
            UnitInfo {
                kind: "SquareTime",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Gram per Centimetre Second
            "GM-PER-CentiM-SEC",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Gram per Square Centimetre
            "GM-PER-CentiM2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Square Centimetre Year
            "GM-PER-CentiM2-YR",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 3.168808781402895e-07,
                offset: 0.0,
            },
        ),
        (
            // Gram per Cubic Centimetre
            "GM-PER-CentiM3",
            UnitInfo {
                kind: "Density",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Cubic Centimetre Bar
            "GM-PER-CentiM3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Gram per Day
            "GM-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Gram per Decilitre
            "GM-PER-DeciL",
            UnitInfo {
                kind: "Density",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Cubic Decimetre
            "GM-PER-DeciM3",
            UnitInfo {
                kind: "Density",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Cubic Decimetre Bar
            "GM-PER-DeciM3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Gram per Gram
            "GM-PER-GM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Hectare
            "GM-PER-HA",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Gram per Hour
            "GM-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Gram per Hectogram
            "GM-PER-HectoGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Gram per Kilogram
            "GM-PER-KiloGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram per Kilometre
            "GM-PER-KiloM",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Gram per Litre
            "GM-PER-L",
            UnitInfo {
                kind: "Density",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Litre Bar
            "GM-PER-L-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Gram per Metre
            "GM-PER-M",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram per Square Metre
            "GM-PER-M2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram per Square Metre Day
            "GM-PER-M2-DAY",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Gram per Square Metre Hour
            "GM-PER-M2-HR",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Gram per Square Metre Year
            "GM-PER-M2-YR",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 3.168808781402895e-11,
                offset: 0.0,
            },
        ),
        (
            // Gram per Cubic Metre
            "GM-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram per Cubic Metre Bar
            "GM-PER-M3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Gram per Minute
            "GM-PER-MIN",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1.6666666666666667e-05,
                offset: 0.0,
            },
        ),
        (
            // Gram per Mole
            "GM-PER-MOL",
            UnitInfo {
                kind: "MolarMass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram per Millilitre
            "GM-PER-MilliL",
            UnitInfo {
                kind: "Density",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Millilitre Bar
            "GM-PER-MilliL-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Gram per Millimetre
            "GM-PER-MilliM",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Millimetre Bar
            "GM-PER-MilliM-BAR",
            UnitInfo {
                kind: "SquareTime",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Gram per Square Millimetre
            "GM-PER-MilliM2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Gram per Second
            "GM-PER-SEC",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram of Carbon
            "GM_Carbon",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram of Carbon per Square Metre Day
            "GM_Carbon-PER-M2-DAY",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Dry Gram
            "GM_DRY",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram Force
            "GM_F",
            UnitInfo {
                kind: "Force",
                multiplier: 0.00980665,
                offset: 0.0,
            },
        ),
        (
            // Gram Force per Square Centimetre
            "GM_F-PER-CentiM2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 98.0665,
                offset: 0.0,
            },
        ),
        (
            // Gram of Nitrogen
            "GM_Nitrogen",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Gram of Nitrogen per Square Metre Day
            "GM_Nitrogen-PER-M2-DAY",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Gon
            "GON",
            UnitInfo {
                kind: "Angle",
                multiplier: 0.015707963267949,
                offset: 0.0,
            },
        ),
        (
            // Grade
            "GR",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Grad
            "GRAD",
            UnitInfo {
                kind: "Angle",
                multiplier: 0.015707963267949,
                offset: 0.0,
            },
        ),
        (
            // Grain
            "GRAIN",
            UnitInfo {
                kind: "Mass",
                multiplier: 6.479891e-05,
                offset: 0.0,
            },
        ),
        (
            // Grain per Imperial Gallon
            "GRAIN-PER-GAL_IMP",
            UnitInfo {
                kind: "Density",
                multiplier: 0.014253767523300242,
                offset: 0.0,
            },
        ),
        (
            // Grain per Us Gallon
            "GRAIN-PER-GAL_US",
            UnitInfo {
                kind: "Density",
                multiplier: 0.017118061045270947,
                offset: 0.0,
            },
        ),
        (
            // Grain per Cubic Metre
            "GRAIN-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 6.479891e-05,
                offset: 0.0,
            },
        ),
        (
            // Gray
            "GRAY",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gray per Hour
            "GRAY-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Gray per Minute
            "GRAY-PER-MIN",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Gray per Second
            "GRAY-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Gross Tonnage
            "GT",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Gibibit
            "GibiBIT",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 744261117.954893,
                offset: 0.0,
            },
        ),
        (
            // Gibibit per Metre
            "GibiBIT-PER-M",
            UnitInfo {
                kind: "LinearBitDensity",
                multiplier: 744261117.954893,
                offset: 0.0,
            },
        ),
        (
            // Gibibit per Square Metre
            "GibiBIT-PER-M2",
            UnitInfo {
                kind: "AreaBitDensity",
                multiplier: 744261117.954893,
                offset: 0.0,
            },
        ),
        (
            // Gibibit per Cubic Metre
            "GibiBIT-PER-M3",
            UnitInfo {
                kind: "VolumetricBitDensity",
                multiplier: 744261117.954893,
                offset: 0.0,
            },
        ),
        (
            // Gibibyte
            "GibiBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5954088943.639144,
                offset: 0.0,
            },
        ),
        (
            // Gigaampere
            "GigaA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigabit
            "GigaBIT",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 693147180.5599453,
                offset: 0.0,
            },
        ),
        (
            // Gigabit per Metre
            "GigaBIT-PER-M",
            UnitInfo {
                kind: "LinearBitDensity",
                multiplier: 693147180.5599453,
                offset: 0.0,
            },
        ),
        (
            // Gigabit per Second
            "GigaBIT-PER-SEC",
            UnitInfo {
                kind: "DataRate",
                multiplier: 693147180.5599453,
                offset: 0.0,
            },
        ),
        (
            // Gigabecquerel
            "GigaBQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigabyte
            "GigaBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5545177444.479563,
                offset: 0.0,
            },
        ),
        (
            // Gigabyte per Second
            "GigaBYTE-PER-SEC",
            UnitInfo {
                kind: "ByteRate",
                multiplier: 5545177444.479563,
                offset: 0.0,
            },
        ),
        (
            // Giga Base Pair
            "GigaBasePair",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigacoulomb
            "GigaC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigacoulomb per Cubic Metre
            "GigaC-PER-M3",
            UnitInfo {
                kind: "ElectricChargeVolumeDensity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Giga Electron Volt
            "GigaEV",
            UnitInfo {
                kind: "Energy",
                multiplier: 1.602176634e-10,
                offset: 0.0,
            },
        ),
        (
            // Giga Floating Point Operations per Second
            "GigaFLOPS",
            UnitInfo {
                kind: "FloatingPointCalculationCapability",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigahertz
            "GigaHZ",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigahertz Metre
            "GigaHZ-M",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigajoule
            "GigaJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigajoule per Hour
            "GigaJ-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 277777.77777777775,
                offset: 0.0,
            },
        ),
        (
            // Gigajoule per Square Metre
            "GigaJ-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigajoule per Second
            "GigaJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Giganewton
            "GigaN",
            UnitInfo {
                kind: "Force",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Giganewton Metre per Square Metre
            "GigaN-M-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigaohm
            "GigaOHM",
            UnitInfo {
                kind: "Resistance",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigaohm Metre
            "GigaOHM-M",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigaohm per Metre
            "GigaOHM-PER-M",
            UnitInfo {
                kind: "LinearResistance",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigapascal
            "GigaPA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigapascal Cubic Centimetre per Gram
            "GigaPA-CentiM3-PER-GM",
            UnitInfo {
                kind: "SpecificModulus",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigavolt
            "GigaV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Giga Volt Ampere
            "GigaVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Giga Volt Ampere Reactive
            "GigaVAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigawatt
            "GigaW",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Gigawatt Hour
            "GigaW-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Henry
            "H",
            UnitInfo {
                kind: "Inductance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Henry per Kiloohm
            "H-PER-KiloOHM",
            UnitInfo {
                kind: "Time",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Henry per Metre
            "H-PER-M",
            UnitInfo {
                kind: "ElectromagneticPermeability",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Henry per Ohm
            "H-PER-OHM",
            UnitInfo {
                kind: "Time",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Hectare
            "HA",
            UnitInfo {
                kind: "Area",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Hartley
            "HART",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 2.302585092994046,
                offset: 0.0,
            },
        ),
        (
            // Hartley per Second
            "HART-PER-SEC",
            UnitInfo {
                kind: "InformationFlowRate",
                multiplier: 2.302585092994046,
                offset: 0.0,
            },
        ),
        (
            // Hefner-kerze
            "HK",
            UnitInfo {
                kind: "LuminousIntensity",
                multiplier: 0.92,
                offset: 0.0,
            },
        ),
        (
            // Horsepower
            "HP",
            UnitInfo {
                kind: "Power",
                multiplier: 745.6999,
                offset: 0.0,
            },
        ),
        (
            // Boiler Horsepower
            "HP_Boiler",
            UnitInfo {
                kind: "Power",
                multiplier: 9809.5,
                offset: 0.0,
            },
        ),
        (
            // Horsepower (brake)
            "HP_Brake",
            UnitInfo {
                kind: "Power",
                multiplier: 9809.5,
                offset: 0.0,
            },
        ),
        (
            // Horsepower (electric)
            "HP_Electric",
            UnitInfo {
                kind: "Power",
                multiplier: 746.0,
                offset: 0.0,
            },
        ),
        (
            // Horsepower (water)
            "HP_H2O",
            UnitInfo {
                kind: "Power",
                multiplier: 746.043,
                offset: 0.0,
            },
        ),
        (
            // Horsepower (metric)
            "HP_Metric",
            UnitInfo {
                kind: "Power",
                multiplier: 735.4988,
                offset: 0.0,
            },
        ),
        (
            // Hour
            "HR",
            UnitInfo {
                kind: "Time",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Hour Square Foot
            "HR-FT2",
            UnitInfo {
                kind: "AreaTime",
                multiplier: 334.450944,
                offset: 0.0,
            },
        ),
        (
            // Hour per Number
            "HR-PER-NUM",
            UnitInfo {
                kind: "TimePerCount",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Hour per Year
            "HR-PER-YR",
            UnitInfo {
                kind: "TimeRatio",
                multiplier: 0.00011407711613050422,
                offset: 0.0,
            },
        ),
        (
            // Sidereal Hour
            "HR_Sidereal",
            UnitInfo {
                kind: "Time",
                multiplier: 3590.17,
                offset: 0.0,
            },
        ),
        (
            // Hundred
            "HUNDRED",
            UnitInfo {
                kind: "Count",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Hertz
            "HZ",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Hertz Metre
            "HZ-M",
            UnitInfo {
                kind: "Speed",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Hertz per Kelvin
            "HZ-PER-K",
            UnitInfo {
                kind: "InverseTimeTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Hertz per Tesla
            "HZ-PER-T",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Hertz per Volt
            "HZ-PER-V",
            UnitInfo {
                kind: "InverseMagneticFlux",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Abhenry
            "H_Ab",
            UnitInfo {
                kind: "Inductance",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Stathenry
            "H_Stat",
            UnitInfo {
                kind: "Inductance",
                multiplier: 898760000000.0,
                offset: 0.0,
            },
        ),
        (
            // Stathenry per Centimetre
            "H_Stat-PER-CentiM",
            UnitInfo {
                kind: "ElectromagneticPermeability",
                multiplier: 89876000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Hectobar
            "HectoBAR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 10000000.0,
                offset: 0.0,
            },
        ),
        (
            // Hectocoulomb
            "HectoC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Hectogram
            "HectoGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Hectolitre
            "HectoL",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Hectometre
            "HectoM",
            UnitInfo {
                kind: "Length",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Hectopascal
            "HectoPA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Hectopascal Litre per Second
            "HectoPA-L-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Hectopascal Cubic Metre per Second
            "HectoPA-M3-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Hectopascal per Bar
            "HectoPA-PER-BAR",
            UnitInfo {
                kind: "PressureRatio",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Hectopascal per Hour
            "HectoPA-PER-HR",
            UnitInfo {
                kind: "ForcePerAreaTime",
                multiplier: 0.027777777777777776,
                offset: 0.0,
            },
        ),
        (
            // Hectopascal per Kelvin
            "HectoPA-PER-K",
            UnitInfo {
                kind: "PressureCoefficient",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Hectopascal per Metre
            "HectoPA-PER-M",
            UnitInfo {
                kind: "PressureGradient",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Hundredweight (UK)
            "Hundredweight_UK",
            UnitInfo {
                kind: "Mass",
                multiplier: 50.80235,
                offset: 0.0,
            },
        ),
        (
            // Hundredweight (US)
            "Hundredweight_US",
            UnitInfo {
                kind: "Mass",
                multiplier: 45.359237,
                offset: 0.0,
            },
        ),
        (
            // Inch
            "IN",
            UnitInfo {
                kind: "Length",
                multiplier: 0.0254,
                offset: 0.0,
            },
        ),
        (
            // Inch Poundal
            "IN-PDL",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.0035116758411504,
                offset: 0.0,
            },
        ),
        (
            // Inch per Degree Fahrenheit
            "IN-PER-DEG_F",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 0.04572,
                offset: 0.0,
            },
        ),
        (
            // Inch per Minute
            "IN-PER-MIN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.00042333333333333334,
                offset: 0.0,
            },
        ),
        (
            // Inch per Revolution
            "IN-PER-REV",
            UnitInfo {
                kind: "Rotary-TranslatoryMotionConversion",
                multiplier: 0.004042535554534142,
                offset: 0.0,
            },
        ),
        (
            // Inch per Second
            "IN-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.0254,
                offset: 0.0,
            },
        ),
        (
            // Inch per Square Second
            "IN-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 0.0254,
                offset: 0.0,
            },
        ),
        (
            // Inch per Year
            "IN-PER-YR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 8.048774304763353e-10,
                offset: 0.0,
            },
        ),
        (
            // Square Inch
            "IN2",
            UnitInfo {
                kind: "Area",
                multiplier: 0.00064516,
                offset: 0.0,
            },
        ),
        (
            // Square Inch per Second
            "IN2-PER-SEC",
            UnitInfo {
                kind: "AreaPerTime",
                multiplier: 0.00064516,
                offset: 0.0,
            },
        ),
        (
            // Cubic Inch
            "IN3",
            UnitInfo {
                kind: "Volume",
                multiplier: 1.6387064e-05,
                offset: 0.0,
            },
        ),
        (
            // Cubic Inch per Hour
            "IN3-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 4.551962222222222e-09,
                offset: 0.0,
            },
        ),
        (
            // Cubic Inch per Pound Mass
            "IN3-PER-LB",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 3.612729200008369e-05,
                offset: 0.0,
            },
        ),
        (
            // Cubic Inch per Minute
            "IN3-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.7311773333333333e-07,
                offset: 0.0,
            },
        ),
        (
            // Cubic Inch per Second
            "IN3-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.6387064e-05,
                offset: 0.0,
            },
        ),
        (
            // Quartic Inch
            "IN4",
            UnitInfo {
                kind: "SecondAxialMomentOfArea",
                multiplier: 4.162314256e-07,
                offset: 0.0,
            },
        ),
        (
            // Individual
            "INDIV",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Inch of Water
            "IN_H2O",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 249.0889,
                offset: 0.0,
            },
        ),
        (
            // Inch of Water (39.2 °F)
            "IN_H2O_39dot2DEG_F",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 249.082,
                offset: 0.0,
            },
        ),
        (
            // Inch of Water (60 °F)
            "IN_H2O_60DEG_F",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 248.84,
                offset: 0.0,
            },
        ),
        (
            // Inch of Mercury
            "IN_HG",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 3386.389,
                offset: 0.0,
            },
        ),
        (
            // Inch of Mercury (32 °F)
            "IN_HG_32DEG_F",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 3386.38,
                offset: 0.0,
            },
        ),
        (
            // Inch of Mercury (60 °F)
            "IN_HG_60DEG_F",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 3376.85,
                offset: 0.0,
            },
        ),
        (
            // International Unit
            "IU",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // International Unit per Litre
            "IU-PER-L",
            UnitInfo {
                kind: "PlasmaLevel",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // International Unit per Milligram
            "IU-PER-MilliGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // International Unit per Millilitre
            "IU-PER-MilliL",
            UnitInfo {
                kind: "PlasmaLevel",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Joule
            "J",
            UnitInfo {
                kind: "Energy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule Metre per Mole
            "J-M-PER-MOL",
            UnitInfo {
                kind: "LengthMolarEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule Square Metre
            "J-M2",
            UnitInfo {
                kind: "TotalAtomicStoppingPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule Square Metre per Kilogram
            "J-M2-PER-KiloGM",
            UnitInfo {
                kind: "TotalMassStoppingPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Square Centimetre
            "J-PER-CentiM2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Square Centimetre Day
            "J-PER-CentiM2-DAY",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 0.11574074074074074,
                offset: 0.0,
            },
        ),
        (
            // Joule per Cubic Centimetre Kelvin
            "J-PER-CentiM3-K",
            UnitInfo {
                kind: "VolumetricHeatCapacity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Day
            "J-PER-DAY",
            UnitInfo {
                kind: "Power",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Joule per Gram
            "J-PER-GM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Gram Degree Celsius
            "J-PER-GM-DEG_C",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Gram Kelvin
            "J-PER-GM-K",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Hour
            "J-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Joule per Kelvin
            "J-PER-K",
            UnitInfo {
                kind: "HeatCapacity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Kilogram
            "J-PER-KiloGM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Kilogram Degree Celsius
            "J-PER-KiloGM-DEG_C",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Kilogram Kelvin
            "J-PER-KiloGM-K",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Kilogram Kelvin Cubic Metre
            "J-PER-KiloGM-K-M3",
            UnitInfo {
                kind: "SpecificHeatVolume",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Kilogram Kelvin Pascal
            "J-PER-KiloGM-K-PA",
            UnitInfo {
                kind: "SpecificHeatPressure",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Metre
            "J-PER-M",
            UnitInfo {
                kind: "TotalLinearStoppingPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Square Metre
            "J-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Cubic Metre
            "J-PER-M3",
            UnitInfo {
                kind: "EnergyDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Cubic Metre Kelvin
            "J-PER-M3-K",
            UnitInfo {
                kind: "VolumetricHeatCapacity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Quartic Metre
            "J-PER-M4",
            UnitInfo {
                kind: "SpectralRadiantEnergyDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Minute
            "J-PER-MIN",
            UnitInfo {
                kind: "Power",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Joule per Mole
            "J-PER-MOL",
            UnitInfo {
                kind: "MolarEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Mole Kelvin
            "J-PER-MOL-K",
            UnitInfo {
                kind: "MolarHeatCapacity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Second
            "J-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Tesla
            "J-PER-T",
            UnitInfo {
                kind: "MagneticAreaMoment",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule per Square Tesla
            "J-PER-T2",
            UnitInfo {
                kind: "EnergyPerSquareMagneticFluxDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule Second
            "J-SEC",
            UnitInfo {
                kind: "AngularImpulse",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Joule Second per Mole
            "J-SEC-PER-MOL",
            UnitInfo {
                kind: "MolarAngularMomentum",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin
            "K",
            UnitInfo {
                kind: "Temperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin Day
            "K-DAY",
            UnitInfo {
                kind: "TimeTemperature",
                multiplier: 86400.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin Metre
            "K-M",
            UnitInfo {
                kind: "LengthTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin Metre per Watt
            "K-M-PER-W",
            UnitInfo {
                kind: "ThermalResistivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin per Hour
            "K-PER-HR",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Kelvin per Kelvin
            "K-PER-K",
            UnitInfo {
                kind: "TemperatureRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin per Metre
            "K-PER-M",
            UnitInfo {
                kind: "TemperatureGradient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin per Minute
            "K-PER-MIN",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Kelvin per Second
            "K-PER-SEC",
            UnitInfo {
                kind: "TemperaturePerTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin per Square Second
            "K-PER-SEC2",
            UnitInfo {
                kind: "TemperaturePerSquareTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin per Tesla
            "K-PER-T",
            UnitInfo {
                kind: "TemperaturePerMagneticFluxDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin per Watt
            "K-PER-W",
            UnitInfo {
                kind: "ThermalResistance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kelvin Second
            "K-SEC",
            UnitInfo {
                kind: "TimeTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Katal
            "KAT",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Katal per Litre
            "KAT-PER-L",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Katal per Cubic Metre
            "KAT-PER-M3",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Katal per Microlitre
            "KAT-PER-MicroL",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kip
            "KIP_F",
            UnitInfo {
                kind: "Force",
                multiplier: 4448.221814841143,
                offset: 0.0,
            },
        ),
        (
            // Kip per Square Inch
            "KIP_F-PER-IN2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 6894757.602518977,
                offset: 0.0,
            },
        ),
        (
            // Knot
            "KN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.5144444444444445,
                offset: 0.0,
            },
        ),
        (
            // Knot per Second
            "KN-PER-SEC",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 0.5144444444444445,
                offset: 0.0,
            },
        ),
        (
            // Kayser
            "KY",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Kibibit
            "KibiBIT",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 709.782712893384,
                offset: 0.0,
            },
        ),
        (
            // Kibibit per Metre
            "KibiBIT-PER-M",
            UnitInfo {
                kind: "LinearBitDensity",
                multiplier: 709.782712893384,
                offset: 0.0,
            },
        ),
        (
            // Kibibit per Square Metre
            "KibiBIT-PER-M2",
            UnitInfo {
                kind: "AreaBitDensity",
                multiplier: 709.782712893384,
                offset: 0.0,
            },
        ),
        (
            // Kibibit per Cubic Metre
            "KibiBIT-PER-M3",
            UnitInfo {
                kind: "VolumetricBitDensity",
                multiplier: 709.782712893384,
                offset: 0.0,
            },
        ),
        (
            // Kibibyte
            "KibiBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5678.261703147072,
                offset: 0.0,
            },
        ),
        (
            // Kiloampere
            "KiloA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloampere Hour
            "KiloA-HR",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 3600000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloampere per Kelvin
            "KiloA-PER-K",
            UnitInfo {
                kind: "ElectricCurrentPerTemperature",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloampere per Metre
            "KiloA-PER-M",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloampere per Square Metre
            "KiloA-PER-M2",
            UnitInfo {
                kind: "ElectricCurrentDensity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilobar
            "KiloBAR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 100000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilobaud
            "KiloBAUD",
            UnitInfo {
                kind: "DigitRate",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilobit
            "KiloBIT",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 693.1471805599454,
                offset: 0.0,
            },
        ),
        (
            // Kilobit per Second
            "KiloBIT-PER-SEC",
            UnitInfo {
                kind: "DataRate",
                multiplier: 693.1471805599454,
                offset: 0.0,
            },
        ),
        (
            // Kilobecquerel
            "KiloBQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilobecquerel per Kilogram
            "KiloBQ-PER-KiloGM",
            UnitInfo {
                kind: "SpecificActivity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo British Thermal Unit (international Definition)
            "KiloBTU_IT",
            UnitInfo {
                kind: "Energy",
                multiplier: 1055055.85262,
                offset: 0.0,
            },
        ),
        (
            // Kilo British Thermal Unit (international Definition) per Square Foot
            "KiloBTU_IT-PER-FT2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 11356526.682226976,
                offset: 0.0,
            },
        ),
        (
            // Kilo British Thermal Unit (international Definition) per Hour
            "KiloBTU_IT-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 293.0710701722222,
                offset: 0.0,
            },
        ),
        (
            // Kilo British Thermal Unit (thermochemical Definition)
            "KiloBTU_TH",
            UnitInfo {
                kind: "Energy",
                multiplier: 1054350.2645,
                offset: 0.0,
            },
        ),
        (
            // Kilo British Thermal Unit (thermochemical Definition) per Hour
            "KiloBTU_TH-PER-HR",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 292.8750734722222,
                offset: 0.0,
            },
        ),
        (
            // Kilobyte
            "KiloBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5545.177444479563,
                offset: 0.0,
            },
        ),
        (
            // Kilobyte per Second
            "KiloBYTE-PER-SEC",
            UnitInfo {
                kind: "DataRate",
                multiplier: 5545.177444479563,
                offset: 0.0,
            },
        ),
        (
            // Kilocoulomb
            "KiloC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocoulomb per Square Metre
            "KiloC-PER-M2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocoulomb per Cubic Metre
            "KiloC-PER-M3",
            UnitInfo {
                kind: "ElectricChargeVolumeDensity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie
            "KiloCAL",
            UnitInfo {
                kind: "Energy",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Centimetre Second Degree Celsius
            "KiloCAL-PER-CentiM-SEC-DEG_C",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 418400.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Square Centimetre
            "KiloCAL-PER-CentiM2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 41840000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Square Centimetre Minute
            "KiloCAL-PER-CentiM2-MIN",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 697333.3333333334,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Square Centimetre Second
            "KiloCAL-PER-CentiM2-SEC",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 41840000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Gram
            "KiloCAL-PER-GM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 4184000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Gram Degree Celsius
            "KiloCAL-PER-GM-DEG_C",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 4184000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Minute
            "KiloCAL-PER-MIN",
            UnitInfo {
                kind: "Power",
                multiplier: 69.73333333333333,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Mole
            "KiloCAL-PER-MOL",
            UnitInfo {
                kind: "MolarEnergy",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Mole Degree Celsius
            "KiloCAL-PER-MOL-DEG_C",
            UnitInfo {
                kind: "MolarHeatCapacity",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie per Second
            "KiloCAL-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo International Table Calorie
            "KiloCAL_IT",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 4186.8,
                offset: 0.0,
            },
        ),
        (
            // Kilo International Table Calorie per Gram Kelvin
            "KiloCAL_IT-PER-GM-K",
            UnitInfo {
                kind: "MassicHeatCapacity",
                multiplier: 4186800.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo International Table Calorie per Hour Metre Degree Celsius
            "KiloCAL_IT-PER-HR-M-DEG_C",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 1.163,
                offset: 0.0,
            },
        ),
        (
            // Kilocalorie (Mean)
            "KiloCAL_Mean",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 4190.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Thermochemical Calorie
            "KiloCAL_TH",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Thermochemical Calorie per Hour
            "KiloCAL_TH-PER-HR",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 1.1622222222222223,
                offset: 0.0,
            },
        ),
        (
            // Kilo Thermochemical Calorie per Minute
            "KiloCAL_TH-PER-MIN",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 69.73333333333333,
                offset: 0.0,
            },
        ),
        (
            // Kilo Thermochemical Calorie per Second
            "KiloCAL_TH-PER-SEC",
            UnitInfo {
                kind: "HeatFlowRate",
                multiplier: 4184.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocandela
            "KiloCD",
            UnitInfo {
                kind: "LuminousIntensity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilocurie
            "KiloCI",
            UnitInfo {
                kind: "Activity",
                multiplier: 37000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Cubic Foot
            "KiloCubicFT",
            UnitInfo {
                kind: "Volume",
                multiplier: 28.316846592,
                offset: 0.0,
            },
        ),
        (
            // Kilo Electron Volt
            "KiloEV",
            UnitInfo {
                kind: "Energy",
                multiplier: 1.602176634e-16,
                offset: 0.0,
            },
        ),
        (
            // Kilo Electron Volt per Micrometre
            "KiloEV-PER-MicroM",
            UnitInfo {
                kind: "LinearEnergyTransfer",
                multiplier: 1.602176634e-10,
                offset: 0.0,
            },
        ),
        (
            // Kilofarad
            "KiloFARAD",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogauss
            "KiloGAUSS",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Kilogram
            "KiloGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Centimetre per Second
            "KiloGM-CentiM-PER-SEC",
            UnitInfo {
                kind: "Impulse",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Square Centimetre
            "KiloGM-CentiM2",
            UnitInfo {
                kind: "MomentOfInertia",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Kelvin
            "KiloGM-K",
            UnitInfo {
                kind: "MassTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Metre
            "KiloGM-M",
            UnitInfo {
                kind: "Unbalance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Metre per Second
            "KiloGM-M-PER-SEC",
            UnitInfo {
                kind: "LinearMomentum",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Metre per Square Second
            "KiloGM-M-PER-SEC2",
            UnitInfo {
                kind: "Force",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Square Metre
            "KiloGM-M2",
            UnitInfo {
                kind: "MomentOfInertia",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Square Metre per Second
            "KiloGM-M2-PER-SEC",
            UnitInfo {
                kind: "AngularImpulse",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram Square Millimetre
            "KiloGM-MilliM2",
            UnitInfo {
                kind: "MomentOfInertia",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Centimetre
            "KiloGM-PER-CentiM2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Cubic Centimetre
            "KiloGM-PER-CentiM3",
            UnitInfo {
                kind: "Density",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Cubic Centimetre Bar
            "KiloGM-PER-CentiM3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Day
            "KiloGM-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Cubic Decimetre
            "KiloGM-PER-DeciM3",
            UnitInfo {
                kind: "Density",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Cubic Decimetre Bar
            "KiloGM-PER-DeciM3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Foot
            "KiloGM-PER-FT2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 10.763910416709722,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Gigajoule
            "KiloGM-PER-GigaJ",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Hectare
            "KiloGM-PER-HA",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Hectare Year
            "KiloGM-PER-HA-YR",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 3.168808781402895e-12,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Hour
            "KiloGM-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Joule
            "KiloGM-PER-J",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Kilogram
            "KiloGM-PER-KiloGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Kilometre
            "KiloGM-PER-KiloM",
            UnitInfo {
                kind: "LinearMass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Kilometre
            "KiloGM-PER-KiloM2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Kilomole
            "KiloGM-PER-KiloMOL",
            UnitInfo {
                kind: "MolarMass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Litre
            "KiloGM-PER-L",
            UnitInfo {
                kind: "Density",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Litre Bar
            "KiloGM-PER-L-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Metre
            "KiloGM-PER-M",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Metre Day
            "KiloGM-PER-M-DAY",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Metre Hour
            "KiloGM-PER-M-HR",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Metre Minute
            "KiloGM-PER-M-MIN",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Metre Second
            "KiloGM-PER-M-SEC",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Metre Square Second
            "KiloGM-PER-M-SEC2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Metre
            "KiloGM-PER-M2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Metre Day
            "KiloGM-PER-M2-DAY",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Metre Pascal Second
            "KiloGM-PER-M2-PA-SEC",
            UnitInfo {
                kind: "VapourPermeance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Metre Second
            "KiloGM-PER-M2-SEC",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Metre Square Second
            "KiloGM-PER-M2-SEC2",
            UnitInfo {
                kind: "PressureLossPerLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Cubic Metre
            "KiloGM-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Cubic Metre Bar
            "KiloGM-PER-M3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Cubic Metre Pascal
            "KiloGM-PER-M3-PA",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Minute
            "KiloGM-PER-MIN",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Mole
            "KiloGM-PER-MOL",
            UnitInfo {
                kind: "MolarMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Mega British Thermal Unit (international Definition)
            "KiloGM-PER-MegaBTU_IT",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 9.478171203133171e-10,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Millimetre
            "KiloGM-PER-MilliM",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Pascal Second Metre
            "KiloGM-PER-PA-SEC-M",
            UnitInfo {
                kind: "VapourPermeability",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Second
            "KiloGM-PER-SEC",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Second Square Metre
            "KiloGM-PER-SEC-M2",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Square Second
            "KiloGM-PER-SEC2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Cubic Second Kelvin
            "KiloGM-PER-SEC3-K",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogram per Year
            "KiloGM-PER-YR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 3.168808781402895e-08,
                offset: 0.0,
            },
        ),
        (
            // Kilo Gram Force
            "KiloGM_F",
            UnitInfo {
                kind: "Force",
                multiplier: 9.80665,
                offset: 0.0,
            },
        ),
        (
            // Kilo Gram Force Metre
            "KiloGM_F-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 9.80665,
                offset: 0.0,
            },
        ),
        (
            // Kilo Gram Force Metre per Square Centimetre
            "KiloGM_F-M-PER-CentiM2",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 98066.5,
                offset: 0.0,
            },
        ),
        (
            // Kilo Gram Force Metre per Second
            "KiloGM_F-M-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 9.80665,
                offset: 0.0,
            },
        ),
        (
            // Kilo Gram Force per Square Centimetre
            "KiloGM_F-PER-CentiM2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 98066.5,
                offset: 0.0,
            },
        ),
        (
            // Kilo Gram Force per Square Metre
            "KiloGM_F-PER-M2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 9.80665,
                offset: 0.0,
            },
        ),
        (
            // Kilo Gram Force per Square Millimetre
            "KiloGM_F-PER-MilliM2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 9806650.0,
                offset: 0.0,
            },
        ),
        (
            // Kilogray
            "KiloGRAY",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilohenry
            "KiloH",
            UnitInfo {
                kind: "Inductance",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilohertz
            "KiloHZ",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilohertz Metre
            "KiloHZ-M",
            UnitInfo {
                kind: "ConductionSpeed",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Thousand Individuals
            "KiloINDIV",
            UnitInfo {
                kind: "Count",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule
            "KiloJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Day
            "KiloJ-PER-DAY",
            UnitInfo {
                kind: "Power",
                multiplier: 0.011574074074074073,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Hour
            "KiloJ-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 0.2777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Kelvin
            "KiloJ-PER-K",
            UnitInfo {
                kind: "EnergyPerTemperature",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Kilogram
            "KiloJ-PER-KiloGM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Kilogram Kelvin
            "KiloJ-PER-KiloGM-K",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Kilovolt
            "KiloJ-PER-KiloV",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Minute
            "KiloJ-PER-MIN",
            UnitInfo {
                kind: "Power",
                multiplier: 16.666666666666668,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Mole
            "KiloJ-PER-MOL",
            UnitInfo {
                kind: "MolarEnergy",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilojoule per Second
            "KiloJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilolitre
            "KiloL",
            UnitInfo {
                kind: "Volume",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Kilolitre per Hour
            "KiloL-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Kilo Pound Mass
            "KiloLB",
            UnitInfo {
                kind: "Mass",
                multiplier: 453.59237,
                offset: 0.0,
            },
        ),
        (
            // Kilo Pound Mass per Hour
            "KiloLB-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.12599788055555555,
                offset: 0.0,
            },
        ),
        (
            // Kilo Pound Force
            "KiloLB_F",
            UnitInfo {
                kind: "Force",
                multiplier: 4448.221814841143,
                offset: 0.0,
            },
        ),
        (
            // Kilo Pound Force Foot per Ampere
            "KiloLB_F-FT-PER-A",
            UnitInfo {
                kind: "MagneticFlux",
                multiplier: 1355.8180091635804,
                offset: 0.0,
            },
        ),
        (
            // Kilo Pound Force Foot per Pound Mass
            "KiloLB_F-FT-PER-LB",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 2989.067054112,
                offset: 0.0,
            },
        ),
        (
            // Kilo Pound Force per Foot
            "KiloLB_F-PER-FT",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 14593.9035919985,
                offset: 0.0,
            },
        ),
        (
            // Kilo Pound Force per Square Inch
            "KiloLB_F-PER-IN2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 6894757.602518977,
                offset: 0.0,
            },
        ),
        (
            // Kilolumen
            "KiloLM",
            UnitInfo {
                kind: "LuminousFlux",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilometre
            "KiloM",
            UnitInfo {
                kind: "Length",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilometre per Day
            "KiloM-PER-DAY",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.011574074074074073,
                offset: 0.0,
            },
        ),
        (
            // Kilometre per Hour
            "KiloM-PER-HR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.2777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Kilometre per Second
            "KiloM-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilometre per Square Second
            "KiloM-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Square Kilometre
            "KiloM2",
            UnitInfo {
                kind: "Area",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Square Kilometre per Square Second
            "KiloM2-PER-SEC2",
            UnitInfo {
                kind: "SpecificModulus",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Kilometre per Square Second
            "KiloM3-PER-SEC2",
            UnitInfo {
                kind: "StandardGravitationalParameter",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Circular Mil
            "KiloMIL_Circ",
            UnitInfo {
                kind: "Area",
                multiplier: 5.067075e-07,
                offset: 0.0,
            },
        ),
        (
            // Kilomole
            "KiloMOL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilomole per Hour
            "KiloMOL-PER-HR",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 0.2777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Kilomole per Kilogram
            "KiloMOL-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilomole per Cubic Metre
            "KiloMOL-PER-M3",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilomole per Minute
            "KiloMOL-PER-MIN",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 16.666666666666668,
                offset: 0.0,
            },
        ),
        (
            // Kilomole per Second
            "KiloMOL-PER-SEC",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton
            "KiloN",
            UnitInfo {
                kind: "Force",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton Metre
            "KiloN-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton Metre per Degree
            "KiloN-M-PER-DEG",
            UnitInfo {
                kind: "TorsionalSpringConstant",
                multiplier: 57295.77951308232,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton Metre per Degree Metre
            "KiloN-M-PER-DEG-M",
            UnitInfo {
                kind: "ModulusOfRotationalSubgradeReaction",
                multiplier: 57295.77951308232,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton Metre per Metre
            "KiloN-M-PER-M",
            UnitInfo {
                kind: "TorquePerLength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton Metre per Square Metre
            "KiloN-M-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton Square Metre
            "KiloN-M2",
            UnitInfo {
                kind: "WarpingMoment",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton per Square Centimetre
            "KiloN-PER-CentiM2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 10000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton per Metre
            "KiloN-PER-M",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton per Square Metre
            "KiloN-PER-M2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton per Cubic Metre
            "KiloN-PER-M3",
            UnitInfo {
                kind: "SpecificWeight",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilonewton per Square Millimetre
            "KiloN-PER-MilliM2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloohm
            "KiloOHM",
            UnitInfo {
                kind: "Resistance",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloohm Metre
            "KiloOHM-M",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloohm per Metre
            "KiloOHM-PER-M",
            UnitInfo {
                kind: "LinearResistance",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilopascal
            "KiloPA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilopascal Square Metre per Gram
            "KiloPA-M2-PER-GM",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilopascal per Bar
            "KiloPA-PER-BAR",
            UnitInfo {
                kind: "PressureRatio",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Kilopascal per Kelvin
            "KiloPA-PER-K",
            UnitInfo {
                kind: "PressureCoefficient",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilopascal per Metre
            "KiloPA-PER-M",
            UnitInfo {
                kind: "PressureGradient",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilopascal per Millimetre
            "KiloPA-PER-MilliM",
            UnitInfo {
                kind: "SpectralRadiantEnergyDensity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilopascal Absolute
            "KiloPA_A",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilopoise
            "KiloPOISE",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Kilopond
            "KiloPOND",
            UnitInfo {
                kind: "Force",
                multiplier: 9.80665,
                offset: 0.0,
            },
        ),
        (
            // Kiloroentgen
            "KiloR",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 0.258,
                offset: 0.0,
            },
        ),
        (
            // Kilosiemens
            "KiloS",
            UnitInfo {
                kind: "Admittance",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilosiemens per Metre
            "KiloS-PER-M",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilosecond
            "KiloSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilotesla
            "KiloT",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilotonne
            "KiloTONNE",
            UnitInfo {
                kind: "Mass",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilotonne per Year
            "KiloTONNE-PER-YR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.03168808781402895,
                offset: 0.0,
            },
        ),
        (
            // Kilo Metric Ton
            "KiloTON_Metric",
            UnitInfo {
                kind: "Mass",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilovolt
            "KiloV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilovolt per Metre
            "KiloV-PER-M",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Volt Ampere
            "KiloVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Volt Ampere Hour
            "KiloVA-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Volt Ampere per Kelvin
            "KiloVA-PER-K",
            UnitInfo {
                kind: "ThermalConductance",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Volt Ampere Reactive
            "KiloVAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilo Volt Ampere Reactive Hour
            "KiloVAR-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilowatt
            "KiloW",
            UnitInfo {
                kind: "Power",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilowatt Hour
            "KiloW-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilowatt Hour per Square Metre
            "KiloW-HR-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 3600000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilowatt per Metre Degree Celsius
            "KiloW-PER-M-DEG_C",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilowatt per Metre Kelvin
            "KiloW-PER-M-K",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilowatt per Square Metre
            "KiloW-PER-M2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilowatt per Square Metre Kelvin
            "KiloW-PER-M2-K",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kilowatt per Ton of Refrigeration
            "KiloW-PER-TON_FG",
            UnitInfo {
                kind: "CoolingPerformanceRatio",
                multiplier: 0.28434512332474515,
                offset: 0.0,
            },
        ),
        (
            // Kiloweber
            "KiloWB",
            UnitInfo {
                kind: "MagneticFlux",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloweber per Metre
            "KiloWB-PER-M",
            UnitInfo {
                kind: "MagneticVectorPotential",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Kiloyear
            "KiloYR",
            UnitInfo {
                kind: "Time",
                multiplier: 31557600000.0,
                offset: 0.0,
            },
        ),
        (
            // Litre
            "L",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Litre per Day
            "L-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Litre per Hectare
            "L-PER-HA",
            UnitInfo {
                kind: "VolumePerArea",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Litre per Hour
            "L-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Litre per Kelvin
            "L-PER-K",
            UnitInfo {
                kind: "VolumeThermalExpansion",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Litre per Kilogram
            "L-PER-KiloGM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Litre per Litre
            "L-PER-L",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Litre per Minute
            "L-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.6666666666666667e-05,
                offset: 0.0,
            },
        ),
        (
            // Litre per Mole
            "L-PER-MOL",
            UnitInfo {
                kind: "MolarRefractivity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Litre per Mole Second
            "L-PER-MOL-SEC",
            UnitInfo {
                kind: "AtmosphericHydroxylationRate",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Litre per Micromole
            "L-PER-MicroMOL",
            UnitInfo {
                kind: "MolarRefractivity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Litre per Second
            "L-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Litre per Second Square Metre
            "L-PER-SEC-M2",
            UnitInfo {
                kind: "VentilationRatePerFloorArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Lambert
            "LA",
            UnitInfo {
                kind: "Luminance",
                multiplier: 0.31830988618,
                offset: 0.0,
            },
        ),
        (
            // Langley
            "LANGLEY",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 41840.0,
                offset: 0.0,
            },
        ),
        (
            // Foot Lambert
            "LA_FT",
            UnitInfo {
                kind: "Luminance",
                multiplier: 3.426259099594588,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass
            "LB",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.45359237,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass Degree Fahrenheit
            "LB-DEG_F",
            UnitInfo {
                kind: "MassTemperature",
                multiplier: 0.2519957611111111,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass Degree Rankine
            "LB-DEG_R",
            UnitInfo {
                kind: "MassTemperature",
                multiplier: 0.2519957611111111,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass Foot per Second
            "LB-FT-PER-SEC",
            UnitInfo {
                kind: "Impulse",
                multiplier: 0.138254954376,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass Square Foot
            "LB-FT2",
            UnitInfo {
                kind: "MomentOfInertia",
                multiplier: 0.0421401100938048,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass Inch
            "LB-IN",
            UnitInfo {
                kind: "LengthMass",
                multiplier: 0.011521246198,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass Inch per Second
            "LB-IN-PER-SEC",
            UnitInfo {
                kind: "Impulse",
                multiplier: 0.011521246198,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass Square Inch
            "LB-IN2",
            UnitInfo {
                kind: "MomentOfInertia",
                multiplier: 0.0002926396534292,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Acre
            "LB-PER-AC",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.0001120851156194456,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Day
            "LB-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 5.249911689814815e-06,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Foot
            "LB-PER-FT",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 1.4881639435695537,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Foot Day
            "LB-PER-FT-DAY",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1.7224119717240207e-05,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Foot Hour
            "LB-PER-FT-HR",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 0.00041337887321376497,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Foot Minute
            "LB-PER-FT-MIN",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 0.024802732392825898,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Foot Second
            "LB-PER-FT-SEC",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1.4881639435695537,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Square Foot
            "LB-PER-FT2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 4.88242763638305,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Cubic Foot
            "LB-PER-FT3",
            UnitInfo {
                kind: "Density",
                multiplier: 16.018463373960138,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Cubic Foot Psi
            "LB-PER-FT3-PSI",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 0.0023232815854335254,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Imperial Gallon
            "LB-PER-GAL_IMP",
            UnitInfo {
                kind: "Density",
                multiplier: 99.7763726631017,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Gallon (UK)
            "LB-PER-GAL_UK",
            UnitInfo {
                kind: "Density",
                multiplier: 99.7763726631017,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Us Gallon
            "LB-PER-GAL_US",
            UnitInfo {
                kind: "Density",
                multiplier: 119.82642731689663,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Hour
            "LB-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.00012599788055555556,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Hour Psi
            "LB-PER-HR-PSI",
            UnitInfo {
                kind: "ThrusterPowerToThrustEfficiency",
                multiplier: 1.827444673464991e-08,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Inch
            "LB-PER-IN",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 17.857967322834646,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Square Inch
            "LB-PER-IN2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 703.0695796391593,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Cubic Inch
            "LB-PER-IN3",
            UnitInfo {
                kind: "Density",
                multiplier: 27679.90471020312,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Cubic Inch Psi
            "LB-PER-IN3-PSI",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 4.014630579629132,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Pound Mass
            "LB-PER-LB",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Cubic Metre
            "LB-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 0.45359237,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Minute
            "LB-PER-MIN",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.007559872833333333,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Minute Psi
            "LB-PER-MIN-PSI",
            UnitInfo {
                kind: "ThrusterPowerToThrustEfficiency",
                multiplier: 1.0964668040789946e-06,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Second
            "LB-PER-SEC",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.45359237,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Second Psi
            "LB-PER-SEC-PSI",
            UnitInfo {
                kind: "ThrusterPowerToThrustEfficiency",
                multiplier: 6.578800824473968e-05,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Yard
            "LB-PER-YD",
            UnitInfo {
                kind: "LinearMass",
                multiplier: 0.4960546478565179,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Square Yard
            "LB-PER-YD2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.5424919595981167,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass per Cubic Yard
            "LB-PER-YD3",
            UnitInfo {
                kind: "Density",
                multiplier: 0.5932764212577829,
                offset: 0.0,
            },
        ),
        (
            // Pound Force
            "LB_F",
            UnitInfo {
                kind: "Force",
                multiplier: 4.448221814841143,
                offset: 0.0,
            },
        ),
        (
            // Pound Force Foot
            "LB_F-FT",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 1.3558180091635803,
                offset: 0.0,
            },
        ),
        (
            // Pound Force Foot per Inch
            "LB_F-FT-PER-IN",
            UnitInfo {
                kind: "LinearTorque",
                multiplier: 53.378661778093715,
                offset: 0.0,
            },
        ),
        (
            // Pound Force Inch
            "LB_F-IN",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.11298483409696503,
                offset: 0.0,
            },
        ),
        (
            // Pound Force Inch per Inch
            "LB_F-IN-PER-IN",
            UnitInfo {
                kind: "LinearTorque",
                multiplier: 4.448221814841143,
                offset: 0.0,
            },
        ),
        (
            // Pound Force per Foot
            "LB_F-PER-FT",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 14.5939035919985,
                offset: 0.0,
            },
        ),
        (
            // Pound Force per Square Foot
            "LB_F-PER-FT2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 47.880261128604005,
                offset: 0.0,
            },
        ),
        (
            // Pound Force per Inch
            "LB_F-PER-IN",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 175.126843103982,
                offset: 0.0,
            },
        ),
        (
            // Pound Force per Square Inch
            "LB_F-PER-IN2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 6894.7576025189765,
                offset: 0.0,
            },
        ),
        (
            // Pound Force per Square Inch Degree Fahrenheit
            "LB_F-PER-IN2-DEG_F",
            UnitInfo {
                kind: "VolumetricHeatCapacity",
                multiplier: 12410.563684534158,
                offset: 0.0,
            },
        ),
        (
            // Pound Force per Square Inch Second
            "LB_F-PER-IN2-SEC",
            UnitInfo {
                kind: "ForcePerAreaTime",
                multiplier: 6894.7576025189765,
                offset: 0.0,
            },
        ),
        (
            // Pound Force per Pound Mass
            "LB_F-PER-LB",
            UnitInfo {
                kind: "ThrustToMassRatio",
                multiplier: 9.80665044,
                offset: 0.0,
            },
        ),
        (
            // Pound Force per Yard
            "LB_F-PER-YD",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 4.864634530666167,
                offset: 0.0,
            },
        ),
        (
            // Pound Force Second per Square Foot
            "LB_F-SEC-PER-FT2",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 47.880261128604005,
                offset: 0.0,
            },
        ),
        (
            // Pound Force Second per Square Inch
            "LB_F-SEC-PER-IN2",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 6894.7576025189765,
                offset: 0.0,
            },
        ),
        (
            // Pound Mass
            "LB_M",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.45359237,
                offset: 0.0,
            },
        ),
        (
            // Pound Troy
            "LB_T",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.3732417216,
                offset: 0.0,
            },
        ),
        (
            // Lumen
            "LM",
            UnitInfo {
                kind: "LuminousFlux",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lumen Hour
            "LM-HR",
            UnitInfo {
                kind: "LuminousEnergy",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Lumen per Square Foot
            "LM-PER-FT2",
            UnitInfo {
                kind: "Luminance",
                multiplier: 10.763910416709722,
                offset: 0.0,
            },
        ),
        (
            // Lumen per Square Metre
            "LM-PER-M2",
            UnitInfo {
                kind: "Luminance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lumen per Watt
            "LM-PER-W",
            UnitInfo {
                kind: "LuminousEfficacy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lumen Second
            "LM-SEC",
            UnitInfo {
                kind: "LuminousEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lux
            "LUX",
            UnitInfo {
                kind: "LuminousFluxPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Lux Hour
            "LUX-HR",
            UnitInfo {
                kind: "LuminousExposure",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Lux Second
            "LUX-SEC",
            UnitInfo {
                kind: "LuminousExposure",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Light Year
            "LY",
            UnitInfo {
                kind: "Length",
                multiplier: 9460730472580800.0,
                offset: 0.0,
            },
        ),
        (
            // Lunar Mass
            "LunarMass",
            UnitInfo {
                kind: "Mass",
                multiplier: 7.346e+22,
                offset: 0.0,
            },
        ),
        (
            // Metre
            "M",
            UnitInfo {
                kind: "Length",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre Kelvin
            "M-K",
            UnitInfo {
                kind: "LengthTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre Kelvin per Watt
            "M-K-PER-W",
            UnitInfo {
                kind: "ThermalResistivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre Kilogram
            "M-KiloGM",
            UnitInfo {
                kind: "LengthMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre Pascal per Second
            "M-PA-PER-SEC",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Day
            "M-PER-DAY",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Metre per Degree Celsius Metre
            "M-PER-DEG_C-M",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Farad
            "M-PER-FARAD",
            UnitInfo {
                kind: "InversePermittivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Hectare
            "M-PER-HA",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Metre per Hour
            "M-PER-HR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Metre per Kelvin
            "M-PER-K",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Square Metre
            "M-PER-M2",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Minute
            "M-PER-MIN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Metre per Radian
            "M-PER-RAD",
            UnitInfo {
                kind: "Rotary-TranslatoryMotionConversion",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Second
            "M-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Square Second
            "M-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Volt Second
            "M-PER-V-SEC",
            UnitInfo {
                kind: "MagneticReluctivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Metre per Year
            "M-PER-YR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 3.168808781402895e-08,
                offset: 0.0,
            },
        ),
        (
            // Square Metre
            "M2",
            UnitInfo {
                kind: "Area",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre Hour Degree Celsius per Kilo International Table Calorie
            "M2-HR-DEG_C-PER-KiloCAL_IT",
            UnitInfo {
                kind: "ThermalInsulance",
                multiplier: 0.8598452278589854,
                offset: 0.0,
            },
        ),
        (
            // Square Metre Hertz
            "M2-HZ",
            UnitInfo {
                kind: "AreaPerTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre Kelvin
            "M2-K",
            UnitInfo {
                kind: "AreaTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre Kelvin per Watt
            "M2-K-PER-W",
            UnitInfo {
                kind: "ThermalInsulance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Gram
            "M2-PER-GM",
            UnitInfo {
                kind: "MassAttenuationCoefficient",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Dry Gram
            "M2-PER-GM_DRY",
            UnitInfo {
                kind: "MassAttenuationCoefficient",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Hectare
            "M2-PER-HA",
            UnitInfo {
                kind: "AreaRatio",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Hour
            "M2-PER-HR",
            UnitInfo {
                kind: "AreaPerTime",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Joule
            "M2-PER-J",
            UnitInfo {
                kind: "SpectralCrossSection",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Kelvin
            "M2-PER-K",
            UnitInfo {
                kind: "AreaThermalExpansion",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Kilogram
            "M2-PER-KiloGM",
            UnitInfo {
                kind: "MassAttenuationCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Kilowatt
            "M2-PER-KiloW",
            UnitInfo {
                kind: "AreaPerPower",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Metre
            "M2-PER-M",
            UnitInfo {
                kind: "AreaPerLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Square Metre
            "M2-PER-M2",
            UnitInfo {
                kind: "AreaRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Mole
            "M2-PER-MOL",
            UnitInfo {
                kind: "MolarAbsorptionCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Newton
            "M2-PER-N",
            UnitInfo {
                kind: "Compressibility",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Second
            "M2-PER-SEC",
            UnitInfo {
                kind: "AreaPerTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Square Second
            "M2-PER-SEC2",
            UnitInfo {
                kind: "SpecificModulus",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Square Second Kelvin
            "M2-PER-SEC2-K",
            UnitInfo {
                kind: "SpecificHeatCapacity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Steradian
            "M2-PER-SR",
            UnitInfo {
                kind: "AngularCrossSection",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Steradian Joule
            "M2-PER-SR-J",
            UnitInfo {
                kind: "SpectralAngularCrossSection",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Volt Second
            "M2-PER-V-SEC",
            UnitInfo {
                kind: "Mobility",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre per Watt
            "M2-PER-W",
            UnitInfo {
                kind: "AreaPerPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Metre Steradian
            "M2-SR",
            UnitInfo {
                kind: "AreaAngle",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre
            "M3",
            UnitInfo {
                kind: "Volume",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Coulomb
            "M3-PER-C",
            UnitInfo {
                kind: "HallCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Day
            "M3-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Hectare
            "M3-PER-HA",
            UnitInfo {
                kind: "VolumePerArea",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Hectare Year
            "M3-PER-HA-YR",
            UnitInfo {
                kind: "SurfaceRelatedVolumeFlow",
                multiplier: 3.168808781402895e-12,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Hour
            "M3-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Kelvin
            "M3-PER-K",
            UnitInfo {
                kind: "VolumeThermalExpansion",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Kilogram
            "M3-PER-KiloGM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Square Metre
            "M3-PER-M2",
            UnitInfo {
                kind: "VolumePerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Cubic Metre
            "M3-PER-M3",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Minute
            "M3-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Mole
            "M3-PER-MOL",
            UnitInfo {
                kind: "MolarRefractivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Mole Second
            "M3-PER-MOL-SEC",
            UnitInfo {
                kind: "AtmosphericHydroxylationRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Second
            "M3-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Second Square Metre
            "M3-PER-SEC-M2",
            UnitInfo {
                kind: "SurfaceRelatedVolumeFlowRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Square Second
            "M3-PER-SEC2",
            UnitInfo {
                kind: "StandardGravitationalParameter",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Cubic Metre per Year
            "M3-PER-YR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 3.168808781402895e-08,
                offset: 0.0,
            },
        ),
        (
            // Quartic Metre
            "M4",
            UnitInfo {
                kind: "SecondAxialMomentOfArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Quintic Metre
            "M5",
            UnitInfo {
                kind: "SectionAreaIntegral",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Sextic Metre
            "M6",
            UnitInfo {
                kind: "WarpingConstant",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mach
            "MACH",
            UnitInfo {
                kind: "MachNumber",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Mesh
            "MESH",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Mho
            "MHO",
            UnitInfo {
                kind: "Admittance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Statmho
            "MHO_Stat",
            UnitInfo {
                kind: "ElectricConductivity",
                multiplier: 1.1126500561e-12,
                offset: 0.0,
            },
        ),
        (
            // International Mile
            "MI",
            UnitInfo {
                kind: "Length",
                multiplier: 1609.344,
                offset: 0.0,
            },
        ),
        (
            // International Mile per Hour
            "MI-PER-HR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.44704,
                offset: 0.0,
            },
        ),
        (
            // International Mile per Minute
            "MI-PER-MIN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 26.8224,
                offset: 0.0,
            },
        ),
        (
            // International Mile per Second
            "MI-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1609.344,
                offset: 0.0,
            },
        ),
        (
            // Square International Mile
            "MI2",
            UnitInfo {
                kind: "Area",
                multiplier: 2589988.110336,
                offset: 0.0,
            },
        ),
        (
            // Cubic International Mile
            "MI3",
            UnitInfo {
                kind: "Volume",
                multiplier: 4168181825.4405794,
                offset: 0.0,
            },
        ),
        (
            // Million
            "MILLION",
            UnitInfo {
                kind: "Count",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mil Angle
            "MIL_Angle",
            UnitInfo {
                kind: "Angle",
                multiplier: 0.0009817477042468104,
                offset: 0.0,
            },
        ),
        (
            // Circular Mil
            "MIL_Circ",
            UnitInfo {
                kind: "Area",
                multiplier: 5.067075e-10,
                offset: 0.0,
            },
        ),
        (
            // Mil
            "MIL_Length",
            UnitInfo {
                kind: "Distance",
                multiplier: 2.54e-05,
                offset: 0.0,
            },
        ),
        (
            // Minute
            "MIN",
            UnitInfo {
                kind: "Time",
                multiplier: 60.0,
                offset: 0.0,
            },
        ),
        (
            // Minute per Kilometre
            "MIN-PER-KiloM",
            UnitInfo {
                kind: "Pace",
                multiplier: 0.06,
                offset: 0.0,
            },
        ),
        (
            // Minute per International Mile
            "MIN-PER-MI",
            UnitInfo {
                kind: "Pace",
                multiplier: 0.03728227153424004,
                offset: 0.0,
            },
        ),
        (
            // Minute per Number
            "MIN-PER-NUM",
            UnitInfo {
                kind: "TimePerCount",
                multiplier: 60.0,
                offset: 0.0,
            },
        ),
        (
            // Minute Angle
            "MIN_Angle",
            UnitInfo {
                kind: "Angle",
                multiplier: 0.000290888209,
                offset: 0.0,
            },
        ),
        (
            // Sidereal Minute
            "MIN_Sidereal",
            UnitInfo {
                kind: "Time",
                multiplier: 59.83617,
                offset: 0.0,
            },
        ),
        (
            // Nautical Mile
            "MI_N",
            UnitInfo {
                kind: "Length",
                multiplier: 1852.0,
                offset: 0.0,
            },
        ),
        (
            // Nautical Mile per Hour
            "MI_N-PER-HR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.5144444444444445,
                offset: 0.0,
            },
        ),
        (
            // Nautical Mile per Minute
            "MI_N-PER-MIN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 30.866666666666667,
                offset: 0.0,
            },
        ),
        (
            // Imperial Mile
            "MI_UK",
            UnitInfo {
                kind: "Length",
                multiplier: 1609.344,
                offset: 0.0,
            },
        ),
        (
            // Cubic Imperial Mile
            "MI_UK3",
            UnitInfo {
                kind: "Volume",
                multiplier: 4168181825.4405794,
                offset: 0.0,
            },
        ),
        (
            // Us Survey Mile
            "MI_US",
            UnitInfo {
                kind: "Length",
                multiplier: 1609.347219,
                offset: 0.0,
            },
        ),
        (
            // Us Survey Mile per Square Second
            "MI_US-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 1609.347219,
                offset: 0.0,
            },
        ),
        (
            // Square Us Survey Mile
            "MI_US2",
            UnitInfo {
                kind: "Area",
                multiplier: 2589998.471303034,
                offset: 0.0,
            },
        ),
        (
            // Month
            "MO",
            UnitInfo {
                kind: "Time",
                multiplier: 2551442.976,
                offset: 0.0,
            },
        ),
        (
            // Month per Number
            "MO-PER-NUM",
            UnitInfo {
                kind: "TimePerCount",
                multiplier: 2551442.976,
                offset: 0.0,
            },
        ),
        (
            // Mohm
            "MOHM",
            UnitInfo {
                kind: "MechanicalMobility",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Mole
            "MOL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole Degree Celsius
            "MOL-DEG_C",
            UnitInfo {
                kind: "TemperatureAmountOfSubstance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole Kelvin
            "MOL-K",
            UnitInfo {
                kind: "TemperatureAmountOfSubstance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Cubic Decimetre
            "MOL-PER-DeciM3",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Gram Hour
            "MOL-PER-GM-HR",
            UnitInfo {
                kind: "BiogeochemicalRate",
                multiplier: 0.2777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Mole per Hour
            "MOL-PER-HR",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Mole per Kilogram
            "MOL-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Kilogram Bar
            "MOL-PER-KiloGM-BAR",
            UnitInfo {
                kind: "AmountOfSubstancePerMassPressure",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Mole per Kilogram Pascal
            "MOL-PER-KiloGM-PA",
            UnitInfo {
                kind: "AmountOfSubstancePerMassPressure",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Litre
            "MOL-PER-L",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Square Metre Day
            "MOL-PER-M2-DAY",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Mole per Square Metre Second
            "MOL-PER-M2-SEC",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Square Metre Second Metre
            "MOL-PER-M2-SEC-M",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Cubic Metre
            "MOL-PER-M3",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Cubic Metre Second
            "MOL-PER-M3-SEC",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Minute
            "MOL-PER-MIN",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Mole per Second
            "MOL-PER-SEC",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Mole per Tonne
            "MOL-PER-TONNE",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Pound Mole
            "MOL_LB",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 453.59237,
                offset: 0.0,
            },
        ),
        (
            // Pound Mole Degree Fahrenheit
            "MOL_LB-DEG_F",
            UnitInfo {
                kind: "MassAmountOfSubstanceTemperature",
                multiplier: 251.9957611111111,
                offset: 0.0,
            },
        ),
        (
            // Pound Mole per Pound Mass
            "MOL_LB-PER-LB",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Pound Mole per Minute
            "MOL_LB-PER-MIN",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 7.559872833333333,
                offset: 0.0,
            },
        ),
        (
            // Pound Mole per Second
            "MOL_LB-PER-SEC",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 453.59237,
                offset: 0.0,
            },
        ),
        (
            // Momme (pearl)
            "MOMME_Pearl",
            UnitInfo {
                kind: "Mass",
                multiplier: 3.75,
                offset: 0.0,
            },
        ),
        (
            // Momme (textile)
            "MOMME_Textile",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.00434,
                offset: 0.0,
            },
        ),
        (
            // Mean Gregorian Month
            "MO_MeanGREGORIAN",
            UnitInfo {
                kind: "Time",
                multiplier: 2629746.0,
                offset: 0.0,
            },
        ),
        (
            // Mean Julian Month
            "MO_MeanJulian",
            UnitInfo {
                kind: "Time",
                multiplier: 2629800.0,
                offset: 0.0,
            },
        ),
        (
            // Synodic Month
            "MO_Synodic",
            UnitInfo {
                kind: "Time",
                multiplier: 2551442.976,
                offset: 0.0,
            },
        ),
        (
            // Maxwell
            "MX",
            UnitInfo {
                kind: "MagneticFlux",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Conventional Metre of Water
            "M_H2O",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 9806.65,
                offset: 0.0,
            },
        ),
        (
            // Mebibit
            "MebiBIT",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 726817.4980028252,
                offset: 0.0,
            },
        ),
        (
            // Mebibit per Metre
            "MebiBIT-PER-M",
            UnitInfo {
                kind: "LinearBitDensity",
                multiplier: 726817.4980028252,
                offset: 0.0,
            },
        ),
        (
            // Mebibit per Square Metre
            "MebiBIT-PER-M2",
            UnitInfo {
                kind: "AreaBitDensity",
                multiplier: 726817.4980028252,
                offset: 0.0,
            },
        ),
        (
            // Mebibit per Cubic Metre
            "MebiBIT-PER-M3",
            UnitInfo {
                kind: "VolumetricBitDensity",
                multiplier: 726817.4980028252,
                offset: 0.0,
            },
        ),
        (
            // Mebibyte
            "MebiBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5814539.9840226015,
                offset: 0.0,
            },
        ),
        (
            // Megaampere
            "MegaA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megaampere per Square Metre
            "MegaA-PER-M2",
            UnitInfo {
                kind: "ElectricCurrentDensity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megabar
            "MegaBAR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 100000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megabaud
            "MegaBAUD",
            UnitInfo {
                kind: "DigitRate",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megabit
            "MegaBIT",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 693147.1805599453,
                offset: 0.0,
            },
        ),
        (
            // Megabit per Second
            "MegaBIT-PER-SEC",
            UnitInfo {
                kind: "DataRate",
                multiplier: 693147.1805599453,
                offset: 0.0,
            },
        ),
        (
            // Megabecquerel
            "MegaBQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megabecquerel per Kilogram
            "MegaBQ-PER-KiloGM",
            UnitInfo {
                kind: "SpecificActivity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega British Thermal Unit (international Definition)
            "MegaBTU_IT",
            UnitInfo {
                kind: "Energy",
                multiplier: 1055055852.62,
                offset: 0.0,
            },
        ),
        (
            // Mega British Thermal Unit (international Definition) per Hour
            "MegaBTU_IT-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 293071.0701722222,
                offset: 0.0,
            },
        ),
        (
            // Megabyte
            "MegaBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5545177.444479562,
                offset: 0.0,
            },
        ),
        (
            // Megabyte per Second
            "MegaBYTE-PER-SEC",
            UnitInfo {
                kind: "ByteRate",
                multiplier: 5545177.444479562,
                offset: 0.0,
            },
        ),
        (
            // Megacoulomb
            "MegaC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megacoulomb per Square Metre
            "MegaC-PER-M2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megacoulomb per Cubic Metre
            "MegaC-PER-M3",
            UnitInfo {
                kind: "ElectricChargeDensity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Us Dollar
            "MegaCCY_USD",
            UnitInfo {
                kind: "Currency",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Us Dollar per Flight
            "MegaCCY_USD-PER-FLIGHT",
            UnitInfo {
                kind: "CurrencyPerFlight",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Electron Volt
            "MegaEV",
            UnitInfo {
                kind: "Energy",
                multiplier: 1.602176634e-13,
                offset: 0.0,
            },
        ),
        (
            // Mega Electron Volt Femtometre
            "MegaEV-FemtoM",
            UnitInfo {
                kind: "LengthEnergy",
                multiplier: 1.602176634e-28,
                offset: 0.0,
            },
        ),
        (
            // Mega Electron Volt per Centimetre
            "MegaEV-PER-CentiM",
            UnitInfo {
                kind: "LinearEnergyTransfer",
                multiplier: 1.602176634e-11,
                offset: 0.0,
            },
        ),
        (
            // Mega Electron Volt per Speed of Light
            "MegaEV-PER-SpeedOfLight",
            UnitInfo {
                kind: "LinearMomentum",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Floating Point Operations per Second
            "MegaFLOPS",
            UnitInfo {
                kind: "FloatingPointCalculationCapability",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megagram
            "MegaGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Megagram per Hectare
            "MegaGM-PER-HA",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Megagram per Hectare Year
            "MegaGM-PER-HA-YR",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 3.168808781402895e-09,
                offset: 0.0,
            },
        ),
        (
            // Megagram per Cubic Metre
            "MegaGM-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Megagray
            "MegaGRAY",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megahertz
            "MegaHZ",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megahertz Metre
            "MegaHZ-M",
            UnitInfo {
                kind: "Speed",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megahertz per Kelvin
            "MegaHZ-PER-K",
            UnitInfo {
                kind: "InverseTimeTemperature",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megahertz per Tesla
            "MegaHZ-PER-T",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Million Individuals
            "MegaINDIV",
            UnitInfo {
                kind: "Count",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Megajoule
            "MegaJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megajoule per Hour
            "MegaJ-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 277.77777777777777,
                offset: 0.0,
            },
        ),
        (
            // Megajoule per Kelvin
            "MegaJ-PER-K",
            UnitInfo {
                kind: "HeatCapacity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megajoule per Kilogram
            "MegaJ-PER-KiloGM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megajoule per Square Metre
            "MegaJ-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megajoule per Square Metre Day
            "MegaJ-PER-M2-DAY",
            UnitInfo {
                kind: "Irradiance",
                multiplier: 11.574074074074074,
                offset: 0.0,
            },
        ),
        (
            // Megajoule per Cubic Metre
            "MegaJ-PER-M3",
            UnitInfo {
                kind: "EnergyDensity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megajoule per Second
            "MegaJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megakelvin
            "MegaK",
            UnitInfo {
                kind: "Temperature",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megalitre
            "MegaL",
            UnitInfo {
                kind: "Volume",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Pound Force
            "MegaLB_F",
            UnitInfo {
                kind: "Force",
                multiplier: 4448221.814841143,
                offset: 0.0,
            },
        ),
        (
            // Meganewton
            "MegaN",
            UnitInfo {
                kind: "Force",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Meganewton Metre
            "MegaN-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Meganewton Metre per Square Metre
            "MegaN-M-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Meganewton per Square Metre
            "MegaN-PER-M2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Meganewton per Cubic Metre
            "MegaN-PER-M3",
            UnitInfo {
                kind: "SpecificWeight",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megaohm
            "MegaOHM",
            UnitInfo {
                kind: "Resistance",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megaohm Kilometre
            "MegaOHM-KiloM",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megaohm Metre
            "MegaOHM-M",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megaohm per Kilometre
            "MegaOHM-PER-KiloM",
            UnitInfo {
                kind: "LinearResistance",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Megaohm per Metre
            "MegaOHM-PER-M",
            UnitInfo {
                kind: "LinearResistance",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megapascal
            "MegaPA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megapascal Litre per Second
            "MegaPA-L-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            "MegaPA-M0dot5",
            UnitInfo {
                kind: "StressIntensityFactor",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Megapascal Cubic Metre per Second
            "MegaPA-M3-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megapascal per Bar
            "MegaPA-PER-BAR",
            UnitInfo {
                kind: "PressureRatio",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Megapascal per Kelvin
            "MegaPA-PER-K",
            UnitInfo {
                kind: "PressureCoefficient",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megapsi
            "MegaPSI",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 6894757602.518976,
                offset: 0.0,
            },
        ),
        (
            // Megasiemens
            "MegaS",
            UnitInfo {
                kind: "Admittance",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megasiemens per Metre
            "MegaS-PER-M",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megasecond
            "MegaSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Ton of Oil Equivalent
            "MegaTOE",
            UnitInfo {
                kind: "Energy",
                multiplier: 41868000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megaton
            "MegaTON",
            UnitInfo {
                kind: "Mass",
                multiplier: 907184740.0,
                offset: 0.0,
            },
        ),
        (
            // Megatonne
            "MegaTONNE",
            UnitInfo {
                kind: "Mass",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megatonne per Year
            "MegaTONNE-PER-YR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 31.68808781402895,
                offset: 0.0,
            },
        ),
        (
            // Megavolt
            "MegaV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megavolt per Metre
            "MegaV-PER-M",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Volt Ampere
            "MegaVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Volt Ampere Hour
            "MegaVA-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Volt Ampere Reactive
            "MegaVAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Mega Volt Ampere Reactive Hour
            "MegaVAR-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megawatt
            "MegaW",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megawatt Hour
            "MegaW-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600000000.0,
                offset: 0.0,
            },
        ),
        (
            // Megayear
            "MegaYR",
            UnitInfo {
                kind: "Time",
                multiplier: 31557600000000.0,
                offset: 0.0,
            },
        ),
        (
            // Microampere
            "MicroA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microampere per Kelvin
            "MicroA-PER-K",
            UnitInfo {
                kind: "ElectricCurrentPerTemperature",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micro Standard Atmosphere
            "MicroATM",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 0.101325,
                offset: 0.0,
            },
        ),
        (
            // Microbar
            "MicroBAR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Microbecquerel
            "MicroBQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microbecquerel per Kilogram
            "MicroBQ-PER-KiloGM",
            UnitInfo {
                kind: "SpecificActivity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microbecquerel per Litre
            "MicroBQ-PER-L",
            UnitInfo {
                kind: "ActivityConcentration",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Microcoulomb
            "MicroC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microcoulomb per Square Metre
            "MicroC-PER-M2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microcoulomb per Cubic Metre
            "MicroC-PER-M3",
            UnitInfo {
                kind: "ElectricChargeDensity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microcurie
            "MicroCI",
            UnitInfo {
                kind: "Activity",
                multiplier: 37000.0,
                offset: 0.0,
            },
        ),
        (
            // Microfarad
            "MicroFARAD",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microfarad per Kilometre
            "MicroFARAD-PER-KiloM",
            UnitInfo {
                kind: "Permittivity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Microfarad per Metre
            "MicroFARAD-PER-M",
            UnitInfo {
                kind: "Permittivity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microgravity
            "MicroG",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 9.80665e-06,
                offset: 0.0,
            },
        ),
        (
            // Microgalileo
            "MicroGALILEO",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Microgram
            "MicroGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Square Centimetre
            "MicroGM-PER-CentiM2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Square Centimetre Week
            "MicroGM-PER-CentiM2-WK",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.6534391534391535e-11,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Decilitre
            "MicroGM-PER-DeciL",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Gram
            "MicroGM-PER-GM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Gram Day
            "MicroGM-PER-GM-DAY",
            UnitInfo {
                kind: "MassSpecificBiogeochemicalRate",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Gram Hour
            "MicroGM-PER-GM-HR",
            UnitInfo {
                kind: "MassSpecificBiogeochemicalRate",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Square Inch
            "MicroGM-PER-IN2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1.5500031000062e-06,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Kilogram
            "MicroGM-PER-KiloGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Litre
            "MicroGM-PER-L",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Litre Day
            "MicroGM-PER-L-DAY",
            UnitInfo {
                kind: "MassConcentrationRateOfChange",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Square Metre Day
            "MicroGM-PER-M2-DAY",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.1574074074074074e-14,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Cubic Metre
            "MicroGM-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Cubic Metre Bar
            "MicroGM-PER-M3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1e-14,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Milligram
            "MicroGM-PER-MilliGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Microgram per Millilitre
            "MicroGM-PER-MilliL",
            UnitInfo {
                kind: "Density",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Microgray
            "MicroGRAY",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microgray per Hour
            "MicroGRAY-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Microgray per Minute
            "MicroGRAY-PER-MIN",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.6666666666666667e-08,
                offset: 0.0,
            },
        ),
        (
            // Microgray per Second
            "MicroGRAY-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microhenry
            "MicroH",
            UnitInfo {
                kind: "Inductance",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microhenry per Kiloohm
            "MicroH-PER-KiloOHM",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Microhenry per Metre
            "MicroH-PER-M",
            UnitInfo {
                kind: "ElectromagneticPermeability",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microhenry per Ohm
            "MicroH-PER-OHM",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microinch
            "MicroIN",
            UnitInfo {
                kind: "Length",
                multiplier: 2.54e-08,
                offset: 0.0,
            },
        ),
        (
            // Microjoule
            "MicroJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microjoule per Second
            "MicroJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microkatal
            "MicroKAT",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microkatal per Litre
            "MicroKAT-PER-L",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Microlitre
            "MicroL",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Microlitre per Litre
            "MicroL-PER-L",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micrometre
            "MicroM",
            UnitInfo {
                kind: "Length",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micrometre per Kelvin
            "MicroM-PER-K",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micrometre per Litre Day
            "MicroM-PER-L-DAY",
            UnitInfo {
                kind: "Flux",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Micrometre per Metre
            "MicroM-PER-M",
            UnitInfo {
                kind: "Gradient",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micrometre per Minute
            "MicroM-PER-MIN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1.6666666666666667e-08,
                offset: 0.0,
            },
        ),
        (
            // Micrometre per Newton
            "MicroM-PER-N",
            UnitInfo {
                kind: "LinearCompressibility",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micrometre per Second
            "MicroM-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micrometre per Square Second
            "MicroM-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Square Micrometre
            "MicroM2",
            UnitInfo {
                kind: "Area",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Cubic Micrometre
            "MicroM3",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Cubic Micrometre per Cubic Metre
            "MicroM3-PER-M3",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Cubic Micrometre per Millilitre
            "MicroM3-PER-MilliL",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Micromho
            "MicroMHO",
            UnitInfo {
                kind: "Admittance",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micromole
            "MicroMOL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Gram
            "MicroMOL-PER-GM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Gram Hour
            "MicroMOL-PER-GM-HR",
            UnitInfo {
                kind: "BiogeochemicalRate",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Gram Second
            "MicroMOL-PER-GM-SEC",
            UnitInfo {
                kind: "BiogeochemicalRate",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Kilogram
            "MicroMOL-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Kilogram Year
            "MicroMOL-PER-KiloGM-YR",
            UnitInfo {
                kind: "BiogeochemicalRate",
                multiplier: 3.168808781402895e-14,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Litre
            "MicroMOL-PER-L",
            UnitInfo {
                kind: "Concentration",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Litre Hour
            "MicroMOL-PER-L-HR",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Square Metre Day
            "MicroMOL-PER-M2-DAY",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Square Metre Hour
            "MicroMOL-PER-M2-HR",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Square Metre Second
            "MicroMOL-PER-M2-SEC",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micromole per Second
            "MicroMOL-PER-SEC",
            UnitInfo {
                kind: "PhotosyntheticPhotonFlux",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Square Micromole per Quartic Metre Square Second
            "MicroMOL2-PER-M4-SEC2",
            UnitInfo {
                kind: "MolarFluxDensityVariance",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Micronewton
            "MicroN",
            UnitInfo {
                kind: "Force",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micronewton Metre
            "MicroN-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micronewton Metre per Square Metre
            "MicroN-M-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microohm
            "MicroOHM",
            UnitInfo {
                kind: "Resistance",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microohm Metre
            "MicroOHM-M",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micropascal
            "MicroPA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micropoise
            "MicroPOISE",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Microradian
            "MicroRAD",
            UnitInfo {
                kind: "Angle",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microsiemens
            "MicroS",
            UnitInfo {
                kind: "Admittance",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microsiemens per Centimetre
            "MicroS-PER-CentiM",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Microsiemens per Metre
            "MicroS-PER-M",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Square Microsiemens per Square Centimetre
            "MicroS2-PER-CentiM2",
            UnitInfo {
                kind: "ConductivityVariance",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Microsecond
            "MicroSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microsievert
            "MicroSV",
            UnitInfo {
                kind: "DoseEquivalent",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microsievert per Hour
            "MicroSV-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Microsievert per Minute
            "MicroSV-PER-MIN",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.6666666666666667e-08,
                offset: 0.0,
            },
        ),
        (
            // Microsievert per Second
            "MicroSV-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microtesla
            "MicroT",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microtorr
            "MicroTORR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 0.000133322,
                offset: 0.0,
            },
        ),
        (
            // Microvolt
            "MicroV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microvolt per Metre
            "MicroV-PER-M",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micro Volt Ampere
            "MicroVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micro Volt Ampere per Kelvin
            "MicroVA-PER-K",
            UnitInfo {
                kind: "ThermalConductance",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Micro Volt Ampere Reactive
            "MicroVAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microwatt
            "MicroW",
            UnitInfo {
                kind: "Power",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Microwatt per Square Centimetre Micrometre Steradian
            "MicroW-PER-CentiM2-MicroM-SR",
            UnitInfo {
                kind: "SpectralRadiance",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Microwatt per Square Metre
            "MicroW-PER-M2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Mil Length
            "MilLength",
            UnitInfo {
                kind: "Length",
                multiplier: 2.54e-05,
                offset: 0.0,
            },
        ),
        (
            // Milliampere
            "MilliA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliampere Hour
            "MilliA-HR",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 3.6,
                offset: 0.0,
            },
        ),
        (
            // Milliampere Hour per Gram
            "MilliA-HR-PER-GM",
            UnitInfo {
                kind: "SpecificElectricCharge",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Milliampere per Inch
            "MilliA-PER-IN",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 0.03937007874015748,
                offset: 0.0,
            },
        ),
        (
            // Milliampere per Kelvin
            "MilliA-PER-K",
            UnitInfo {
                kind: "ElectricCurrentPerTemperature",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliampere per Millimetre
            "MilliA-PER-MilliM",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Milliampere Second
            "MilliA-SEC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliarcsecond
            "MilliARCSEC",
            UnitInfo {
                kind: "Angle",
                multiplier: 4.84813681e-09,
                offset: 0.0,
            },
        ),
        (
            // Millibar
            "MilliBAR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Millibar Litre per Second
            "MilliBAR-L-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Millibar Cubic Metre per Second
            "MilliBAR-M3-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Millibar per Bar
            "MilliBAR-PER-BAR",
            UnitInfo {
                kind: "PressureRatio",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millibar per Kelvin
            "MilliBAR-PER-K",
            UnitInfo {
                kind: "VolumetricHeatCapacity",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Milli Bar Absolute
            "MilliBAR_A",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Millibecquerel
            "MilliBQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millibecquerel per Gram
            "MilliBQ-PER-GM",
            UnitInfo {
                kind: "SpecificActivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Millibecquerel per Kilogram
            "MilliBQ-PER-KiloGM",
            UnitInfo {
                kind: "SpecificActivity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millibecquerel per Litre
            "MilliBQ-PER-L",
            UnitInfo {
                kind: "ActivityConcentration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Millicoulomb
            "MilliC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millicoulomb per Kilogram
            "MilliC-PER-KiloGM",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millicoulomb per Square Metre
            "MilliC-PER-M2",
            UnitInfo {
                kind: "ElectricChargePerArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millicoulomb per Cubic Metre
            "MilliC-PER-M3",
            UnitInfo {
                kind: "ElectricChargeVolumeDensity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millicandela
            "MilliCD",
            UnitInfo {
                kind: "LuminousIntensity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millicurie
            "MilliCI",
            UnitInfo {
                kind: "Activity",
                multiplier: 37000000.0,
                offset: 0.0,
            },
        ),
        (
            // Millidarcy
            "MilliDARCY",
            UnitInfo {
                kind: "HydraulicPermeability",
                multiplier: 9.869233e-16,
                offset: 0.0,
            },
        ),
        (
            // Milli Degree Celsius
            "MilliDEG_C",
            UnitInfo {
                kind: "Temperature",
                multiplier: 0.001,
                offset: 273150.0,
            },
        ),
        (
            // Milliequivalent
            "MilliEQ",
            UnitInfo {
                kind: "ReactiveCharge",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliequivalent per Hectogram
            "MilliEQ-PER-HectoGM",
            UnitInfo {
                kind: "ReactiveChargePerMass",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Millifarad
            "MilliFARAD",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milligravity
            "MilliG",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 0.00980665,
                offset: 0.0,
            },
        ),
        (
            // Milligalileo
            "MilliGALILEO",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Milligram
            "MilliGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Square Centimetre
            "MilliGM-PER-CentiM2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Day
            "MilliGM-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Decilitre
            "MilliGM-PER-DeciL",
            UnitInfo {
                kind: "Density",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Square Decimetre
            "MilliGM-PER-DeciM2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Gram
            "MilliGM-PER-GM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Gram Hour
            "MilliGM-PER-GM-HR",
            UnitInfo {
                kind: "MassSpecificBiogeochemicalRate",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Hectare
            "MilliGM-PER-HA",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1e-10,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Hour
            "MilliGM-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Kilogram
            "MilliGM-PER-KiloGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Kilogram Day
            "MilliGM-PER-KiloGM-DAY",
            UnitInfo {
                kind: "MassSpecificBiogeochemicalRate",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Litre
            "MilliGM-PER-L",
            UnitInfo {
                kind: "Density",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Metre
            "MilliGM-PER-M",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Square Metre
            "MilliGM-PER-M2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Square Metre Day
            "MilliGM-PER-M2-DAY",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Square Metre Hour
            "MilliGM-PER-M2-HR",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Square Metre Second
            "MilliGM-PER-M2-SEC",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Cubic Metre
            "MilliGM-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Cubic Metre Bar
            "MilliGM-PER-M3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 1e-11,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Minute
            "MilliGM-PER-MIN",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1.6666666666666667e-08,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Millilitre
            "MilliGM-PER-MilliL",
            UnitInfo {
                kind: "Density",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Milligram per Second
            "MilliGM-PER-SEC",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Milligray
            "MilliGRAY",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milligray per Hour
            "MilliGRAY-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Milligray per Minute
            "MilliGRAY-PER-MIN",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.6666666666666667e-05,
                offset: 0.0,
            },
        ),
        (
            // Milligray per Second
            "MilliGRAY-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millihenry
            "MilliH",
            UnitInfo {
                kind: "Inductance",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millihenry per Kiloohm
            "MilliH-PER-KiloOHM",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Millihenry per Ohm
            "MilliH-PER-OHM",
            UnitInfo {
                kind: "Time",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millihertz
            "MilliHZ",
            UnitInfo {
                kind: "Frequency",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliinch
            "MilliIN",
            UnitInfo {
                kind: "Length",
                multiplier: 2.54e-05,
                offset: 0.0,
            },
        ),
        (
            // Millijoule
            "MilliJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millijoule per Gram
            "MilliJ-PER-GM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Millijoule per Square Metre
            "MilliJ-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millijoule per Second
            "MilliJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millikelvin
            "MilliK",
            UnitInfo {
                kind: "Temperature",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millikelvin per Kelvin
            "MilliK-PER-K",
            UnitInfo {
                kind: "TemperatureRatio",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millikatal
            "MilliKAT",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millikatal per Litre
            "MilliKAT-PER-L",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Millilitre
            "MilliL",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Square Centimetre Minute
            "MilliL-PER-CentiM2-MIN",
            UnitInfo {
                kind: "VolumetricFlux",
                multiplier: 0.00016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Square Centimetre Second
            "MilliL-PER-CentiM2-SEC",
            UnitInfo {
                kind: "VolumetricFlux",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Day
            "MilliL-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Gram
            "MilliL-PER-GM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Hour
            "MilliL-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Kelvin
            "MilliL-PER-K",
            UnitInfo {
                kind: "VolumeThermalExpansion",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Kilogram
            "MilliL-PER-KiloGM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Litre
            "MilliL-PER-L",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Cubic Metre
            "MilliL-PER-M3",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Minute
            "MilliL-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.6666666666666667e-08,
                offset: 0.0,
            },
        ),
        (
            // Millilitre per Second
            "MilliL-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Millimetre
            "MilliM",
            UnitInfo {
                kind: "Length",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Day
            "MilliM-PER-DAY",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Degree Celsius Metre
            "MilliM-PER-DEG_C-M",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Hour
            "MilliM-PER-HR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Kelvin
            "MilliM-PER-K",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Metre
            "MilliM-PER-M",
            UnitInfo {
                kind: "Gradient",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Minute
            "MilliM-PER-MIN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 1.6666666666666667e-05,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Second
            "MilliM-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Square Second
            "MilliM-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimetre per Year
            "MilliM-PER-YR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 3.168808781402895e-11,
                offset: 0.0,
            },
        ),
        (
            // Square Millimetre
            "MilliM2",
            UnitInfo {
                kind: "Area",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Square Millimetre per Second
            "MilliM2-PER-SEC",
            UnitInfo {
                kind: "AreaPerTime",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Millimetre
            "MilliM3",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Cubic Millimetre per Gram
            "MilliM3-PER-GM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Millimetre per Kilogram
            "MilliM3-PER-KiloGM",
            UnitInfo {
                kind: "SpecificVolume",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Cubic Millimetre per Cubic Metre
            "MilliM3-PER-M3",
            UnitInfo {
                kind: "VolumeFraction",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Quartic Millimetre
            "MilliM4",
            UnitInfo {
                kind: "SecondAxialMomentOfArea",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Millimole
            "MilliMOL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimole per Gram
            "MilliMOL-PER-GM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Millimole per Kilogram
            "MilliMOL-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimole per Litre
            "MilliMOL-PER-L",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Millimole per Square Metre Day
            "MilliMOL-PER-M2-DAY",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Millimole per Square Metre Hour
            "MilliMOL-PER-M2-HR",
            UnitInfo {
                kind: "MolarFluxDensity",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Millimole per Square Metre Second
            "MilliMOL-PER-M2-SEC",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimole per Cubic Metre
            "MilliMOL-PER-M3",
            UnitInfo {
                kind: "Concentration",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millimole per Cubic Metre Day
            "MilliMOL-PER-M3-DAY",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1.1574074074074074e-08,
                offset: 0.0,
            },
        ),
        (
            // Conventional Millimetre of Water
            "MilliM_H2O",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 9.80665,
                offset: 0.0,
            },
        ),
        (
            // Millimetre of Mercury
            "MilliM_HG",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 133.322387415,
                offset: 0.0,
            },
        ),
        (
            // Millimetre of Mercury - Absolute
            "MilliM_HGA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 133.322387415,
                offset: 0.0,
            },
        ),
        (
            // Millinewton
            "MilliN",
            UnitInfo {
                kind: "Force",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millinewton Metre
            "MilliN-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millinewton Metre per Square Metre
            "MilliN-M-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millinewton per Metre
            "MilliN-PER-M",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliohm
            "MilliOHM",
            UnitInfo {
                kind: "Resistance",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliohm Metre
            "MilliOHM-M",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliohm per Metre
            "MilliOHM-PER-M",
            UnitInfo {
                kind: "LinearResistance",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliosmole
            "MilliOSM",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Milliosmole per Kilogram
            "MilliOSM-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Millipascal
            "MilliPA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millipascal per Metre
            "MilliPA-PER-M",
            UnitInfo {
                kind: "PressureGradient",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millipascal Second
            "MilliPA-SEC",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millipascal Second per Bar
            "MilliPA-SEC-PER-BAR",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Milliroentgen
            "MilliR",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 2.58e-07,
                offset: 0.0,
            },
        ),
        (
            // Milliradian
            "MilliRAD",
            UnitInfo {
                kind: "Angle",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millirad
            "MilliRAD_R",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Millirad per Hour
            "MilliRAD_R-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 2.777777777777778e-09,
                offset: 0.0,
            },
        ),
        (
            // Milli Roentgen Equivalent Man
            "MilliR_man",
            UnitInfo {
                kind: "DoseEquivalent",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Millisiemens
            "MilliS",
            UnitInfo {
                kind: "Admittance",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millisiemens per Centimetre
            "MilliS-PER-CentiM",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Millisiemens per Metre
            "MilliS-PER-M",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millisecond
            "MilliSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millisievert
            "MilliSV",
            UnitInfo {
                kind: "DoseEquivalent",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millisievert per Hour
            "MilliSV-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Millisievert per Minute
            "MilliSV-PER-MIN",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.6666666666666667e-05,
                offset: 0.0,
            },
        ),
        (
            // Millisievert per Second
            "MilliSV-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millitesla
            "MilliT",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millitorr
            "MilliTORR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 0.133322,
                offset: 0.0,
            },
        ),
        (
            // Millivolt
            "MilliV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millivolt per Metre
            "MilliV-PER-M",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Millivolt per Minute
            "MilliV-PER-MIN",
            UnitInfo {
                kind: "PowerPerElectricCharge",
                multiplier: 1.6666666666666667e-05,
                offset: 0.0,
            },
        ),
        (
            // Milli Volt Ampere
            "MilliVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milli Volt Ampere per Kelvin
            "MilliVA-PER-K",
            UnitInfo {
                kind: "ThermalConductance",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milli Volt Ampere Reactive
            "MilliVAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliwatt
            "MilliW",
            UnitInfo {
                kind: "Power",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliwatt per Square Metre
            "MilliW-PER-M2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Milliwatt per Square Metre Nanometre
            "MilliW-PER-M2-NanoM",
            UnitInfo {
                kind: "ForcePerAreaTime",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Milliwatt per Square Metre Nanometre Steradian
            "MilliW-PER-M2-NanoM-SR",
            UnitInfo {
                kind: "SpectralRadiance",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Milliwatt per Milligram
            "MilliW-PER-MilliGM",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Milliweber
            "MilliWB",
            UnitInfo {
                kind: "MagneticFlux",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Newton
            "N",
            UnitInfo {
                kind: "Force",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Centimetre
            "N-CentiM",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre
            "N-M",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Ampere
            "N-M-PER-A",
            UnitInfo {
                kind: "MagneticFlux",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Arcminute
            "N-M-PER-ARCMIN",
            UnitInfo {
                kind: "TorsionalSpringConstant",
                multiplier: 3437.7467668344025,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Degree
            "N-M-PER-DEG",
            UnitInfo {
                kind: "TorsionalSpringConstant",
                multiplier: 57.29577951308232,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Degree Metre
            "N-M-PER-DEG-M",
            UnitInfo {
                kind: "ModulusOfRotationalSubgradeReaction",
                multiplier: 57.29577951308232,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Kilogram
            "N-M-PER-KiloGM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Metre
            "N-M-PER-M",
            UnitInfo {
                kind: "TorquePerLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Metre Radian
            "N-M-PER-M-RAD",
            UnitInfo {
                kind: "ModulusOfRotationalSubgradeReaction",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Square Metre
            "N-M-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Minute Angle
            "N-M-PER-MIN_Angle",
            UnitInfo {
                kind: "TorsionalSpringConstant",
                multiplier: 3437.7467668344025,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre per Radian
            "N-M-PER-RAD",
            UnitInfo {
                kind: "TorsionalSpringConstant",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre Second
            "N-M-SEC",
            UnitInfo {
                kind: "AngularImpulse",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre Second per Metre
            "N-M-SEC-PER-M",
            UnitInfo {
                kind: "LinearMomentum",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Metre Second per Radian
            "N-M-SEC-PER-RAD",
            UnitInfo {
                kind: "AngularMomentumPerAngle",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Square Metre
            "N-M2",
            UnitInfo {
                kind: "WarpingMoment",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Square Metre per Ampere
            "N-M2-PER-A",
            UnitInfo {
                kind: "MagneticDipoleMoment",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Ampere
            "N-PER-A",
            UnitInfo {
                kind: "MagneticFluxPerLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Coulomb
            "N-PER-C",
            UnitInfo {
                kind: "ForcePerElectricCharge",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Centimetre
            "N-PER-CentiM",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Square Centimetre
            "N-PER-CentiM2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Kilogram
            "N-PER-KiloGM",
            UnitInfo {
                kind: "ThrustToMassRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Metre
            "N-PER-M",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Square Metre
            "N-PER-M2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Cubic Metre
            "N-PER-M3",
            UnitInfo {
                kind: "SpecificWeight",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Millimetre
            "N-PER-MilliM",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Square Millimetre
            "N-PER-MilliM2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Newton per Radian
            "N-PER-RAD",
            UnitInfo {
                kind: "ForcePerAngle",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Second
            "N-SEC",
            UnitInfo {
                kind: "LinearMomentum",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Second per Metre
            "N-SEC-PER-M",
            UnitInfo {
                kind: "MassPerTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Second per Square Metre
            "N-SEC-PER-M2",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Second per Cubic Metre
            "N-SEC-PER-M3",
            UnitInfo {
                kind: "SpecificAcousticImpedance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Newton Second per Radian
            "N-SEC-PER-RAD",
            UnitInfo {
                kind: "MomentumPerAngle",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Nat
            "NAT",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Nat per Second
            "NAT-PER-SEC",
            UnitInfo {
                kind: "InformationFlowRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Normal Cubic Metre
            "NCM",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Normal Cubic Metre
            "NCM_1ATM_0DEG_C_NL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Neper
            "NP",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Net Tonnage
            "NT",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Nephelometry Turbidity Unit
            "NTU",
            UnitInfo {
                kind: "Turbidity",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Number
            "NUM",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Hectare
            "NUM-PER-HA",
            UnitInfo {
                kind: "ParticleFluence",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Number per Hectare Year
            "NUM-PER-HA-YR",
            UnitInfo {
                kind: "Flux",
                multiplier: 3.168808781402895e-12,
                offset: 0.0,
            },
        ),
        (
            // Number per Hour
            "NUM-PER-HR",
            UnitInfo {
                kind: "Frequency",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Number per Square Kilometre
            "NUM-PER-KiloM2",
            UnitInfo {
                kind: "ParticleFluence",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Number per Litre
            "NUM-PER-L",
            UnitInfo {
                kind: "NumberDensity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Metre
            "NUM-PER-M",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Square Metre
            "NUM-PER-M2",
            UnitInfo {
                kind: "ParticleFluence",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Square Metre Day
            "NUM-PER-M2-DAY",
            UnitInfo {
                kind: "Flux",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Number per Cubic Metre
            "NUM-PER-M3",
            UnitInfo {
                kind: "NumberDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Minute
            "NUM-PER-MIN",
            UnitInfo {
                kind: "CountRate",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Number per Microlitre
            "NUM-PER-MicroL",
            UnitInfo {
                kind: "NumberDensity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Millilitre
            "NUM-PER-MilliL",
            UnitInfo {
                kind: "NumberDensity",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Cubic Millimetre
            "NUM-PER-MilliM3",
            UnitInfo {
                kind: "NumberDensity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Nanolitre
            "NUM-PER-NanoL",
            UnitInfo {
                kind: "NumberDensity",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Picolitre
            "NUM-PER-PicoL",
            UnitInfo {
                kind: "NumberDensity",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Second
            "NUM-PER-SEC",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Number per Year
            "NUM-PER-YR",
            UnitInfo {
                kind: "Frequency",
                multiplier: 3.168808781402895e-08,
                offset: 0.0,
            },
        ),
        (
            // Nanoampere
            "NanoA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanoampere per Kelvin
            "NanoA-PER-K",
            UnitInfo {
                kind: "ElectricCurrentPerTemperature",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanobecquerel
            "NanoBQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanobecquerel per Litre
            "NanoBQ-PER-L",
            UnitInfo {
                kind: "ActivityConcentration",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Nanocoulomb
            "NanoC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanofarad
            "NanoFARAD",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanofarad per Metre
            "NanoFARAD-PER-M",
            UnitInfo {
                kind: "Permittivity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanogram
            "NanoGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Square Centimetre
            "NanoGM-PER-CentiM2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Square Centimetre Day
            "NanoGM-PER-CentiM2-DAY",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 1.1574074074074073e-13,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Day
            "NanoGM-PER-DAY",
            UnitInfo {
                kind: "MassPerTime",
                multiplier: 1.1574074074074074e-17,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Decilitre
            "NanoGM-PER-DeciL",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Kilogram
            "NanoGM-PER-KiloGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Litre
            "NanoGM-PER-L",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Square Metre Pascal Second
            "NanoGM-PER-M2-PA-SEC",
            UnitInfo {
                kind: "VapourPermeance",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Cubic Metre
            "NanoGM-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Microlitre
            "NanoGM-PER-MicroL",
            UnitInfo {
                kind: "Density",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Milligram
            "NanoGM-PER-MilliGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Nanogram per Millilitre
            "NanoGM-PER-MilliL",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Nanogray
            "NanoGRAY",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanogray per Hour
            "NanoGRAY-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 2.777777777777778e-13,
                offset: 0.0,
            },
        ),
        (
            // Nanogray per Minute
            "NanoGRAY-PER-MIN",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.6666666666666667e-11,
                offset: 0.0,
            },
        ),
        (
            // Nanogray per Second
            "NanoGRAY-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanohenry
            "NanoH",
            UnitInfo {
                kind: "Inductance",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanohenry per Metre
            "NanoH-PER-M",
            UnitInfo {
                kind: "ElectromagneticPermeability",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanojoule
            "NanoJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanojoule per Second
            "NanoJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanokatal
            "NanoKAT",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanokatal per Litre
            "NanoKAT-PER-L",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Nanolitre
            "NanoL",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Nanometre
            "NanoM",
            UnitInfo {
                kind: "Length",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanometre per Centimetre Megapascal
            "NanoM-PER-CentiM-MegaPA",
            UnitInfo {
                kind: "StressOpticCoefficient",
                multiplier: 1e-13,
                offset: 0.0,
            },
        ),
        (
            // Nanometre per Centimetre Psi
            "NanoM-PER-CentiM-PSI",
            UnitInfo {
                kind: "StressOpticCoefficient",
                multiplier: 1.4503773122272686e-11,
                offset: 0.0,
            },
        ),
        (
            // Nanometre per Millimetre Megapascal
            "NanoM-PER-MilliM-MegaPA",
            UnitInfo {
                kind: "StressOpticCoefficient",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Square Nanometre
            "NanoM2",
            UnitInfo {
                kind: "Area",
                multiplier: 1e-18,
                offset: 0.0,
            },
        ),
        (
            // Nanomole
            "NanoMOL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Cubic Centimetre Hour
            "NanoMOL-PER-CentiM3-HR",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Gram
            "NanoMOL-PER-GM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Gram Hour
            "NanoMOL-PER-GM-HR",
            UnitInfo {
                kind: "BiogeochemicalRate",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Gram Second
            "NanoMOL-PER-GM-SEC",
            UnitInfo {
                kind: "BiogeochemicalRate",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Kilogram
            "NanoMOL-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Litre
            "NanoMOL-PER-L",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Litre Day
            "NanoMOL-PER-L-DAY",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1.1574074074074074e-11,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Litre Hour
            "NanoMOL-PER-L-HR",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 2.7777777777777777e-10,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Square Metre Day
            "NanoMOL-PER-M2-DAY",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 1.1574074074074074e-14,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Square Metre Second
            "NanoMOL-PER-M2-SEC",
            UnitInfo {
                kind: "MolarFluxDensity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanomole per Microgram Hour
            "NanoMOL-PER-MicroGM-HR",
            UnitInfo {
                kind: "BiogeochemicalRate",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Nanonewton
            "NanoN",
            UnitInfo {
                kind: "Force",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanonewton Metre per Square Metre
            "NanoN-M-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanoohm
            "NanoOHM",
            UnitInfo {
                kind: "ElectricalResistance",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanoohm Metre
            "NanoOHM-M",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanosiemens
            "NanoS",
            UnitInfo {
                kind: "Admittance",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanosiemens per Centimetre
            "NanoS-PER-CentiM",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Nanosiemens per Metre
            "NanoS-PER-M",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanosecond
            "NanoSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanosievert
            "NanoSV",
            UnitInfo {
                kind: "DoseEquivalent",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanosievert per Hour
            "NanoSV-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 2.777777777777778e-13,
                offset: 0.0,
            },
        ),
        (
            // Nanosievert per Minute
            "NanoSV-PER-MIN",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.6666666666666667e-11,
                offset: 0.0,
            },
        ),
        (
            // Nanosievert per Second
            "NanoSV-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanotesla
            "NanoT",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanovolt
            "NanoV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nano Volt Ampere
            "NanoVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nano Volt Ampere Reactive
            "NanoVAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanowatt
            "NanoW",
            UnitInfo {
                kind: "Power",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Nanowatt per Square Metre
            "NanoW-PER-M2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Oct
            "OCT",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Octet
            "OCTET",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 5.545177444479562,
                offset: 0.0,
            },
        ),
        (
            // Octet per Second
            "OCTET-PER-SEC",
            UnitInfo {
                kind: "BitRate",
                multiplier: 5.545177444479562,
                offset: 0.0,
            },
        ),
        (
            // Oersted
            "OERSTED",
            UnitInfo {
                kind: "MagneticFieldStrength",
                multiplier: 79.5774715,
                offset: 0.0,
            },
        ),
        (
            // Oersted Centimetre
            "OERSTED-CentiM",
            UnitInfo {
                kind: "MagnetomotiveForce",
                multiplier: 0.795774715,
                offset: 0.0,
            },
        ),
        (
            // Ohm
            "OHM",
            UnitInfo {
                kind: "Resistance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ohm Centimetre
            "OHM-CentiM",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Ohm Kilometre
            "OHM-KiloM",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Ohm Metre
            "OHM-M",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ohm Square Metre per Metre
            "OHM-M2-PER-M",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ohm Circular Mil per Foot
            "OHM-MIL_Circ-PER-FT",
            UnitInfo {
                kind: "Resistivity",
                multiplier: 1.6624261811023623e-09,
                offset: 0.0,
            },
        ),
        (
            // Ohm per Kilometre
            "OHM-PER-KiloM",
            UnitInfo {
                kind: "LinearResistance",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Ohm per Metre
            "OHM-PER-M",
            UnitInfo {
                kind: "LinearResistance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Ohm per International Mile
            "OHM-PER-MI",
            UnitInfo {
                kind: "LinearResistance",
                multiplier: 0.0006213711922373339,
                offset: 0.0,
            },
        ),
        (
            // Abohm
            "OHM_Ab",
            UnitInfo {
                kind: "Resistance",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Statohm
            "OHM_Stat",
            UnitInfo {
                kind: "Resistance",
                multiplier: 898760000000.0,
                offset: 0.0,
            },
        ),
        (
            // Okta
            "OKTA",
            UnitInfo {
                kind: "AmountOfCloudCover",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // One
            "ONE",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // One per One
            "ONE-PER-ONE",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Osmole
            "OSM",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass
            "OZ",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.028349523125,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass Foot
            "OZ-FT",
            UnitInfo {
                kind: "LengthMass",
                multiplier: 0.0086409346485,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass Inch
            "OZ-IN",
            UnitInfo {
                kind: "LengthMass",
                multiplier: 0.000720077887375,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Day
            "OZ-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 3.2811948061342594e-07,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Square Foot
            "OZ-PER-FT2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.30515172727394063,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Imperial Gallon
            "OZ-PER-GAL_IMP",
            UnitInfo {
                kind: "Density",
                multiplier: 6.236023291443856,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Gallon (UK)
            "OZ-PER-GAL_UK",
            UnitInfo {
                kind: "Density",
                multiplier: 6.236023291443856,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Us Gallon
            "OZ-PER-GAL_US",
            UnitInfo {
                kind: "Density",
                multiplier: 7.489151707306039,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Hour
            "OZ-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 7.874867534722223e-06,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Square Inch
            "OZ-PER-IN2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 43.94184872744746,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Cubic Inch
            "OZ-PER-IN3",
            UnitInfo {
                kind: "Density",
                multiplier: 1729.994044387695,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Minute
            "OZ-PER-MIN",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.00047249205208333334,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Second
            "OZ-PER-SEC",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.028349523125,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Square Yard
            "OZ-PER-YD2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.03390574747488229,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass per Cubic Yard
            "OZ-PER-YD3",
            UnitInfo {
                kind: "Density",
                multiplier: 0.03707977632861143,
                offset: 0.0,
            },
        ),
        (
            // Imperial Ounce Force
            "OZ_F",
            UnitInfo {
                kind: "Force",
                multiplier: 0.278013875,
                offset: 0.0,
            },
        ),
        (
            // Imperial Ounce Force Inch
            "OZ_F-IN",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.007061552425,
                offset: 0.0,
            },
        ),
        (
            // Imperial Ounce Force per Cubic Inch
            "OZ_F-PER-IN3",
            UnitInfo {
                kind: "SpecificWeight",
                multiplier: 16965.447562784888,
                offset: 0.0,
            },
        ),
        (
            // Ounce Mass
            "OZ_M",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.028349523125,
                offset: 0.0,
            },
        ),
        (
            // Ounce Troy
            "OZ_TROY",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.0311034768,
                offset: 0.0,
            },
        ),
        (
            // Fluid Ounce (UK)
            "OZ_VOL_UK",
            UnitInfo {
                kind: "Volume",
                multiplier: 2.84130625e-05,
                offset: 0.0,
            },
        ),
        (
            // Fluid Ounce (UK) per Day
            "OZ_VOL_UK-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 3.288548900462963e-10,
                offset: 0.0,
            },
        ),
        (
            // Fluid Ounce (UK) per Hour
            "OZ_VOL_UK-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 7.892517361111111e-09,
                offset: 0.0,
            },
        ),
        (
            // Fluid Ounce (UK) per Minute
            "OZ_VOL_UK-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 4.7355104166666667e-07,
                offset: 0.0,
            },
        ),
        (
            // Fluid Ounce (UK) per Second
            "OZ_VOL_UK-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.84130625e-05,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Ounce
            "OZ_VOL_US",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 2.95735296e-05,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Ounce per Day
            "OZ_VOL_US-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 3.4228622222222224e-10,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Ounce per Hour
            "OZ_VOL_US-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 8.214869333333333e-09,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Ounce per Minute
            "OZ_VOL_US-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 4.9289216e-07,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Ounce per Second
            "OZ_VOL_US-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.95735296e-05,
                offset: 0.0,
            },
        ),
        (
            // Pascal
            "PA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal Litre per Second
            "PA-L-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            "PA-M0dot5",
            UnitInfo {
                kind: "StressIntensityFactor",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal Square Metre per Kilogram
            "PA-M2-PER-KiloGM",
            UnitInfo {
                kind: "BurstFactor",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal Cubic Metre per Second
            "PA-M3-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal per Bar
            "PA-PER-BAR",
            UnitInfo {
                kind: "PressureRatio",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Pascal per Hour
            "PA-PER-HR",
            UnitInfo {
                kind: "ForcePerAreaTime",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Pascal per Kelvin
            "PA-PER-K",
            UnitInfo {
                kind: "PressureCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal per Metre
            "PA-PER-M",
            UnitInfo {
                kind: "SpectralRadiantEnergyDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal per Minute
            "PA-PER-MIN",
            UnitInfo {
                kind: "ForcePerAreaTime",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Pascal per Second
            "PA-PER-SEC",
            UnitInfo {
                kind: "ForcePerAreaTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal Second
            "PA-SEC",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal Second per Bar
            "PA-SEC-PER-BAR",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Pascal Second per Litre
            "PA-SEC-PER-L",
            UnitInfo {
                kind: "PressureInRelationToVolumeFlowRate",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal Second per Metre
            "PA-SEC-PER-M",
            UnitInfo {
                kind: "AcousticImpedance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Pascal Second per Cubic Metre
            "PA-SEC-PER-M3",
            UnitInfo {
                kind: "PressureInRelationToVolumeFlowRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Pascal Second
            "PA2-SEC",
            UnitInfo {
                kind: "SoundExposure",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Parsec
            "PARSEC",
            UnitInfo {
                kind: "Length",
                multiplier: 30856780000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Pica
            "PCA",
            UnitInfo {
                kind: "Length",
                multiplier: 0.0042333,
                offset: 0.0,
            },
        ),
        (
            // Poundal
            "PDL",
            UnitInfo {
                kind: "Force",
                multiplier: 0.138254954376,
                offset: 0.0,
            },
        ),
        (
            // Poundal Foot
            "PDL-FT",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.0421401100938048,
                offset: 0.0,
            },
        ),
        (
            // Poundal Inch
            "PDL-IN",
            UnitInfo {
                kind: "MomentOfForce",
                multiplier: 0.0035116758411504,
                offset: 0.0,
            },
        ),
        (
            // Poundal per Square Foot
            "PDL-PER-FT2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1.4881639435695537,
                offset: 0.0,
            },
        ),
        (
            // Poundal per Inch
            "PDL-PER-IN",
            UnitInfo {
                kind: "ForcePerLength",
                multiplier: 5.44310844,
                offset: 0.0,
            },
        ),
        (
            // Poundal per Square Inch
            "PDL-PER-IN2",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 214.29560787401576,
                offset: 0.0,
            },
        ),
        (
            // Poundal Second per Square Foot
            "PDL-SEC-PER-FT2",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 1.4881639435695537,
                offset: 0.0,
            },
        ),
        (
            // Poundal Second per Square Inch
            "PDL-SEC-PER-IN2",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 214.29560787401576,
                offset: 0.0,
            },
        ),
        (
            // Pennyweight
            "PENNYWEIGHT",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.00155517384,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Angstrom
            "PER-ANGSTROM",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 10000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Bar
            "PER-BAR",
            UnitInfo {
                kind: "Compressibility",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Centimetre
            "PER-CentiM",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Cubic Centimetre
            "PER-CentiM3",
            UnitInfo {
                kind: "InverseVolume",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Day
            "PER-DAY",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1.1574074074074073e-05,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Degree Celsius
            "PER-DEG_C",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Degree Fahrenheit
            "PER-DEG_F",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 1.8,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Electron Volt Cubic Metre
            "PER-EV-M3",
            UnitInfo {
                kind: "EnergyDensityOfStates",
                multiplier: 6.241509074460762e+18,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Electron Volt
            "PER-EV2",
            UnitInfo {
                kind: "InverseSquareEnergy",
                multiplier: 3.8956435526576046e+37,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Cubic Foot
            "PER-FT3",
            UnitInfo {
                kind: "InverseVolume",
                multiplier: 35.31466672148859,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Gram
            "PER-GM",
            UnitInfo {
                kind: "InverseMass",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Giga Electron Volt
            "PER-GigaEV2",
            UnitInfo {
                kind: "InverseSquareEnergy",
                multiplier: 3.895643552657605e+19,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Henry
            "PER-H",
            UnitInfo {
                kind: "Reluctance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Hour
            "PER-HR",
            UnitInfo {
                kind: "Frequency",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Inch
            "PER-IN",
            UnitInfo {
                kind: "Repetency",
                multiplier: 39.37007874015748,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Inch
            "PER-IN2",
            UnitInfo {
                kind: "ParticleFluence",
                multiplier: 1550.0031000062,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Cubic Inch
            "PER-IN3",
            UnitInfo {
                kind: "InverseVolume",
                multiplier: 61023.74409473228,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Joule
            "PER-J",
            UnitInfo {
                kind: "InverseEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Joule Cubic Metre
            "PER-J-M3",
            UnitInfo {
                kind: "EnergyDensityOfStates",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Joule
            "PER-J2",
            UnitInfo {
                kind: "InverseSquareEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Kelvin
            "PER-K",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Kilogram
            "PER-KiloGM",
            UnitInfo {
                kind: "InverseMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Kilogram
            "PER-KiloGM2",
            UnitInfo {
                kind: "InverseSquareMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Kilometre
            "PER-KiloM",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Kilo Volt Ampere Hour
            "PER-KiloVA-HR",
            UnitInfo {
                kind: "InverseEnergy",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Litre
            "PER-L",
            UnitInfo {
                kind: "InverseVolume",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Pound Mass
            "PER-LB",
            UnitInfo {
                kind: "InverseMass",
                multiplier: 2.2046226218487757,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Metre
            "PER-M",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Metre Kelvin
            "PER-M-K",
            UnitInfo {
                kind: "InverseLengthTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Metre Nanometre
            "PER-M-NanoM",
            UnitInfo {
                kind: "ParticleFluence",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Metre
            "PER-M2",
            UnitInfo {
                kind: "ParticleFluence",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Metre Second
            "PER-M2-SEC",
            UnitInfo {
                kind: "Flux",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Cubic Metre
            "PER-M3",
            UnitInfo {
                kind: "InverseVolume",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Cubic Metre Second
            "PER-M3-SEC",
            UnitInfo {
                kind: "Slowing-DownDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Minute
            "PER-MIN",
            UnitInfo {
                kind: "Frequency",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Month
            "PER-MO",
            UnitInfo {
                kind: "Frequency",
                multiplier: 3.9193507729016165e-07,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Mole
            "PER-MOL",
            UnitInfo {
                kind: "InverseAmountOfSubstance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Megakelvin
            "PER-MegaK",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Megapascal
            "PER-MegaPA",
            UnitInfo {
                kind: "Compressibility",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Micrometre
            "PER-MicroM",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Milligram
            "PER-MilliGM",
            UnitInfo {
                kind: "InverseMass",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Millilitre
            "PER-MilliL",
            UnitInfo {
                kind: "InverseVolume",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Millimetre
            "PER-MilliM",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Cubic Millimetre
            "PER-MilliM3",
            UnitInfo {
                kind: "InverseVolume",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Millisecond
            "PER-MilliSEC",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Nanometre
            "PER-NanoM",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Ounce Mass
            "PER-OZ",
            UnitInfo {
                kind: "InverseMass",
                multiplier: 35.27396194958041,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Pascal
            "PER-PA",
            UnitInfo {
                kind: "Compressibility",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Psi
            "PER-PSI",
            UnitInfo {
                kind: "StressOpticCoefficient",
                multiplier: 0.00014503773122272686,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Picometre
            "PER-PicoM",
            UnitInfo {
                kind: "InverseLength",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Planck Mass
            "PER-PlanckMass2",
            UnitInfo {
                kind: "InverseSquareMass",
                multiplier: 2111089287176721.8,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Second
            "PER-SEC",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Second Square Metre
            "PER-SEC-M2",
            UnitInfo {
                kind: "Flux",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Second Square Metre Steradian
            "PER-SEC-M2-SR",
            UnitInfo {
                kind: "PhotonRadiance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Second Cubic Metre
            "PER-SEC-M3",
            UnitInfo {
                kind: "Slowing-DownDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Second Steradian
            "PER-SEC-SR",
            UnitInfo {
                kind: "PhotonIntensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Square Second
            "PER-SEC2",
            UnitInfo {
                kind: "InverseSquareTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Tesla Metre
            "PER-T-M",
            UnitInfo {
                kind: "MagneticReluctivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Tesla Second
            "PER-T-SEC",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Ton
            "PER-TON",
            UnitInfo {
                kind: "InverseMass",
                multiplier: 0.001102311310924388,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Tonne
            "PER-TONNE",
            UnitInfo {
                kind: "InverseMass",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Volt
            "PER-V",
            UnitInfo {
                kind: "ReciprocalVoltage",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Volt Ampere Second
            "PER-VA-SEC",
            UnitInfo {
                kind: "InverseEnergy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Weber
            "PER-WB",
            UnitInfo {
                kind: "InverseMagneticFlux",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Week
            "PER-WK",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1.6534391534391535e-06,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Cubic Yard
            "PER-YD3",
            UnitInfo {
                kind: "InverseVolume",
                multiplier: 1.3079506193143922,
                offset: 0.0,
            },
        ),
        (
            // Reciprocal Year
            "PER-YR",
            UnitInfo {
                kind: "Frequency",
                multiplier: 3.168808781402895e-08,
                offset: 0.0,
            },
        ),
        (
            // Percent
            "PERCENT",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Percent Square Foot per Pound Force Second
            "PERCENT-FT2-PER-LB_F-SEC",
            UnitInfo {
                kind: "Fluidity",
                multiplier: 0.0002088543329607267,
                offset: 0.0,
            },
        ),
        (
            // Percent Square Inch per Pound Force Second
            "PERCENT-IN2-PER-LB_F-SEC",
            UnitInfo {
                kind: "Fluidity",
                multiplier: 1.4503773122272687e-06,
                offset: 0.0,
            },
        ),
        (
            // Percent per Bar
            "PERCENT-PER-BAR",
            UnitInfo {
                kind: "Compressibility",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Percent per Day
            "PERCENT-PER-DAY",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1.1574074074074074e-07,
                offset: 0.0,
            },
        ),
        (
            // Percent per Degree Celsius
            "PERCENT-PER-DEG_C",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Percent per Decakelvin
            "PERCENT-PER-DecaK",
            UnitInfo {
                kind: "LinearExpansionCoefficient",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Percent per Hour
            "PERCENT-PER-HR",
            UnitInfo {
                kind: "Frequency",
                multiplier: 2.777777777777778e-06,
                offset: 0.0,
            },
        ),
        (
            // Percent per Hectobar
            "PERCENT-PER-HectoBAR",
            UnitInfo {
                kind: "Compressibility",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Percent per Inch
            "PERCENT-PER-IN",
            UnitInfo {
                kind: "Repetency",
                multiplier: 0.3937007874015748,
                offset: 0.0,
            },
        ),
        (
            // Percent per Kelvin
            "PERCENT-PER-K",
            UnitInfo {
                kind: "LinearExpansionCoefficient",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Percent per Metre
            "PERCENT-PER-M",
            UnitInfo {
                kind: "AttenuationCoefficient",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Percent per Month
            "PERCENT-PER-MO",
            UnitInfo {
                kind: "Frequency",
                multiplier: 3.9193507729016164e-09,
                offset: 0.0,
            },
        ),
        (
            // Percent per Millimetre
            "PERCENT-PER-MilliM",
            UnitInfo {
                kind: "Repetency",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Percent per Second
            "PERCENT-PER-SEC",
            UnitInfo {
                kind: "RateOfChange",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Percent per Week
            "PERCENT-PER-WK",
            UnitInfo {
                kind: "RateOfChange",
                multiplier: 1.6534391534391535e-08,
                offset: 0.0,
            },
        ),
        (
            // Percent per Year
            "PERCENT-PER-YR",
            UnitInfo {
                kind: "RateOfChange",
                multiplier: 3.168808781402895e-10,
                offset: 0.0,
            },
        ),
        (
            // Percent Relative Humidity
            "PERCENT_RH",
            UnitInfo {
                kind: "RelativeHumidity",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Relative Electromagnetic Permeability
            "PERMEABILITY_EM_REL",
            UnitInfo {
                kind: "ElectromagneticPermeabilityRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Relative Permeability
            "PERMEABILITY_REL",
            UnitInfo {
                kind: "PermeabilityRatio",
                multiplier: 1.25663706e-06,
                offset: 0.0,
            },
        ),
        (
            // Permille
            "PERMILLE",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Permille per Psi
            "PERMILLE-PER-PSI",
            UnitInfo {
                kind: "Compressibility",
                multiplier: 1.4503773122272687e-07,
                offset: 0.0,
            },
        ),
        (
            // Relative Permittivity
            "PERMITTIVITY_REL",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 8.854187817e-12,
                offset: 0.0,
            },
        ),
        (
            // Metric Perm
            "PERM_Metric",
            UnitInfo {
                kind: "VapourPermeance",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // U.s. Perm
            "PERM_US",
            UnitInfo {
                kind: "VapourPermeance",
                multiplier: 5.72135e-11,
                offset: 0.0,
            },
        ),
        (
            // Pferdestaerke
            "PFERDESTAERKE",
            UnitInfo {
                kind: "Power",
                multiplier: 735.49875,
                offset: 0.0,
            },
        ),
        (
            // Pfund
            "PFUND",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.5,
                offset: 0.0,
            },
        ),
        (
            // Acidity
            "PH",
            UnitInfo {
                kind: "Acidity",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Phon
            "PHON",
            UnitInfo {
                kind: "SoundPressureLevel",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Phot
            "PHOT",
            UnitInfo {
                kind: "LuminousFluxPerArea",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Imperial Pint
            "PINT",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.00056826125,
                offset: 0.0,
            },
        ),
        (
            // Pint (UK)
            "PINT_UK",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.00056826125,
                offset: 0.0,
            },
        ),
        (
            // Pint (UK) per Day
            "PINT_UK-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 6.577097800925926e-09,
                offset: 0.0,
            },
        ),
        (
            // Pint (UK) per Hour
            "PINT_UK-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.5785034722222223e-07,
                offset: 0.0,
            },
        ),
        (
            // Pint (UK) per Minute
            "PINT_UK-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 9.471020833333334e-06,
                offset: 0.0,
            },
        ),
        (
            // Pint (UK) per Second
            "PINT_UK-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.00056826125,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Pint
            "PINT_US",
            UnitInfo {
                kind: "LiquidVolume",
                multiplier: 0.000473176473,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Pint per Day
            "PINT_US-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 5.476579548611111e-09,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Pint per Hour
            "PINT_US-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.3143790916666668e-07,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Pint per Minute
            "PINT_US-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 7.88627455e-06,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Pint per Second
            "PINT_US-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.000473176473,
                offset: 0.0,
            },
        ),
        (
            // Us Dry Pint
            "PINT_US_DRY",
            UnitInfo {
                kind: "DryVolume",
                multiplier: 0.000550610471,
                offset: 0.0,
            },
        ),
        (
            // Pixel (area)
            "PIXEL_AREA",
            UnitInfo {
                kind: "Area",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Pixel (count)
            "PIXEL_COUNT",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Peck (UK)
            "PK_UK",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.009092181,
                offset: 0.0,
            },
        ),
        (
            // Peck (UK) per Day
            "PK_UK-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.0523357638888889e-07,
                offset: 0.0,
            },
        ),
        (
            // Peck (UK) per Hour
            "PK_UK-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.525605833333333e-06,
                offset: 0.0,
            },
        ),
        (
            // Peck (UK) per Minute
            "PK_UK-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.00015153635,
                offset: 0.0,
            },
        ),
        (
            // Peck (UK) per Second
            "PK_UK-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.009092181,
                offset: 0.0,
            },
        ),
        (
            // Us Peck
            "PK_US_DRY",
            UnitInfo {
                kind: "DryVolume",
                multiplier: 0.00880976754,
                offset: 0.0,
            },
        ),
        (
            // Us Peck per Day
            "PK_US_DRY-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.0196490208333333e-07,
                offset: 0.0,
            },
        ),
        (
            // Us Peck per Hour
            "PK_US_DRY-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.44715765e-06,
                offset: 0.0,
            },
        ),
        (
            // Us Peck per Minute
            "PK_US_DRY-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.000146829459,
                offset: 0.0,
            },
        ),
        (
            // Us Peck per Second
            "PK_US_DRY-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.00880976754,
                offset: 0.0,
            },
        ),
        (
            // Poise
            "POISE",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Poise per Bar
            "POISE-PER-BAR",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Poise per Pascal
            "POISE-PER-PA",
            UnitInfo {
                kind: "Time",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Pond
            "POND",
            UnitInfo {
                kind: "Force",
                multiplier: 0.00980665,
                offset: 0.0,
            },
        ),
        (
            // Parts per Billion
            "PPB",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Parts per Million
            "PPM",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Parts per Million per Kelvin
            "PPM-PER-K",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Part per Quadrillion
            "PPQ",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 1e-16,
                offset: 0.0,
            },
        ),
        (
            // Part per Trillion
            "PPT",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Parts per Thousand
            "PPTH",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 0.001,
                offset: 0.0,
            },
        ),
        (
            // Parts per Thousand per Hour
            "PPTH-PER-HR",
            UnitInfo {
                kind: "Frequency",
                multiplier: 2.7777777777777776e-07,
                offset: 0.0,
            },
        ),
        (
            // Parts per Ten Million
            "PPTM",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Parts per Ten Million per Kelvin
            "PPTM-PER-K",
            UnitInfo {
                kind: "ThermalExpansionCoefficient",
                multiplier: 1e-07,
                offset: 0.0,
            },
        ),
        (
            // Psi
            "PSI",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 6894.7576025189765,
                offset: 0.0,
            },
        ),
        (
            // Psi Cubic Inch per Second
            "PSI-IN3-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 0.11298483409696503,
                offset: 0.0,
            },
        ),
        (
            // Psi Litre per Second
            "PSI-L-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 6.894757602518976,
                offset: 0.0,
            },
        ),
        (
            // Psi Cubic Metre per Second
            "PSI-M3-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 6894.7576025189765,
                offset: 0.0,
            },
        ),
        (
            // Psi per Inch
            "PSI-PER-IN",
            UnitInfo {
                kind: "PressureGradient",
                multiplier: 271447.14970547153,
                offset: 0.0,
            },
        ),
        (
            // Psi per Psi
            "PSI-PER-PSI",
            UnitInfo {
                kind: "PressureRatio",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Psi Cubic Yard per Second
            "PSI-YD3-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 5271.420419628001,
                offset: 0.0,
            },
        ),
        (
            // Practical Salinity Unit
            "PSU",
            UnitInfo {
                kind: "DimensionlessRatio",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Point
            "PT",
            UnitInfo {
                kind: "Length",
                multiplier: 2.54e-05,
                offset: 0.0,
            },
        ),
        (
            // Big Point
            "PT_BIG",
            UnitInfo {
                kind: "Distance",
                multiplier: 0.0003527778,
                offset: 0.0,
            },
        ),
        (
            // Pebibit
            "PebiBIT",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 87267165460724.6,
                offset: 0.0,
            },
        ),
        (
            // Pebibit per Metre
            "PebiBIT-PER-M",
            UnitInfo {
                kind: "LinearBitDensity",
                multiplier: 87267165460724.6,
                offset: 0.0,
            },
        ),
        (
            // Pebibit per Square Metre
            "PebiBIT-PER-M2",
            UnitInfo {
                kind: "AreaBitDensity",
                multiplier: 87267165460724.6,
                offset: 0.0,
            },
        ),
        (
            // Pebibit per Cubic Metre
            "PebiBIT-PER-M3",
            UnitInfo {
                kind: "VolumetricBitDensity",
                multiplier: 87267165460724.6,
                offset: 0.0,
            },
        ),
        (
            // Pebibyte
            "PebiBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 698137323685796.8,
                offset: 0.0,
            },
        ),
        (
            // Petaampere
            "PetaA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Petabit
            "PetaBIT",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 693147180559945.2,
                offset: 0.0,
            },
        ),
        (
            // Petabit per Second
            "PetaBIT-PER-SEC",
            UnitInfo {
                kind: "DataRate",
                multiplier: 693147180559945.2,
                offset: 0.0,
            },
        ),
        (
            // Petabecquerel
            "PetaBQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Petabyte
            "PetaBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5545177444479562.0,
                offset: 0.0,
            },
        ),
        (
            // Petacoulomb
            "PetaC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Peta Floating Point Operations per Second
            "PetaFLOPS",
            UnitInfo {
                kind: "FloatingPointCalculationCapability",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Petahertz
            "PetaHZ",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Petajoule
            "PetaJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Petajoule per Second
            "PetaJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Petavolt
            "PetaV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Peta Volt Ampere
            "PetaVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Petawatt
            "PetaW",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Picoampere
            "PicoA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picocoulomb
            "PicoC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picofarad
            "PicoFARAD",
            UnitInfo {
                kind: "Capacitance",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picofarad per Metre
            "PicoFARAD-PER-M",
            UnitInfo {
                kind: "Permittivity",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picogram
            "PicoGM",
            UnitInfo {
                kind: "Mass",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Picogram per Gram
            "PicoGM-PER-GM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picogram per Kilogram
            "PicoGM-PER-KiloGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Picogram per Litre
            "PicoGM-PER-L",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picogram per Milligram
            "PicoGM-PER-MilliGM",
            UnitInfo {
                kind: "MassRatio",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Picogram per Millilitre
            "PicoGM-PER-MilliL",
            UnitInfo {
                kind: "Density",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Picohenry
            "PicoH",
            UnitInfo {
                kind: "Inductance",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picojoule
            "PicoJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picojoule per Second
            "PicoJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picokatal
            "PicoKAT",
            UnitInfo {
                kind: "CatalyticActivity",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picokatal per Litre
            "PicoKAT-PER-L",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Picolitre
            "PicoL",
            UnitInfo {
                kind: "Volume",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Picometre
            "PicoM",
            UnitInfo {
                kind: "Length",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picomole
            "PicoMOL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picomole per Kilogram
            "PicoMOL-PER-KiloGM",
            UnitInfo {
                kind: "AmountOfSubstancePerMass",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picomole per Litre
            "PicoMOL-PER-L",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1e-09,
                offset: 0.0,
            },
        ),
        (
            // Picomole per Litre Day
            "PicoMOL-PER-L-DAY",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1.1574074074074074e-14,
                offset: 0.0,
            },
        ),
        (
            // Picomole per Litre Hour
            "PicoMOL-PER-L-HR",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 2.777777777777778e-13,
                offset: 0.0,
            },
        ),
        (
            // Picomole per Square Metre Day
            "PicoMOL-PER-M2-DAY",
            UnitInfo {
                kind: "PhotosyntheticPhotonFluxDensity",
                multiplier: 1.1574074074074074e-17,
                offset: 0.0,
            },
        ),
        (
            // Picomole per Cubic Metre
            "PicoMOL-PER-M3",
            UnitInfo {
                kind: "Concentration",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picomole per Cubic Metre Second
            "PicoMOL-PER-M3-SEC",
            UnitInfo {
                kind: "CatalyticActivityConcentration",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picopascal
            "PicoPA",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picopascal per Kilometre
            "PicoPA-PER-KiloM",
            UnitInfo {
                kind: "SpectralRadiantEnergyDensity",
                multiplier: 1e-15,
                offset: 0.0,
            },
        ),
        (
            // Picosiemens
            "PicoS",
            UnitInfo {
                kind: "Admittance",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picosiemens per Metre
            "PicoS-PER-M",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picosecond
            "PicoSEC",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picovolt
            "PicoV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Pico Volt Ampere
            "PicoVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Pico Volt Ampere Reactive
            "PicoVAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picowatt
            "PicoW",
            UnitInfo {
                kind: "Power",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Picowatt per Square Metre
            "PicoW-PER-M2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 1e-12,
                offset: 0.0,
            },
        ),
        (
            // Planck Area
            "PlanckArea",
            UnitInfo {
                kind: "Area",
                multiplier: 2.61223e-71,
                offset: 0.0,
            },
        ),
        (
            // Planck Charge
            "PlanckCharge",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1.87554587e-18,
                offset: 0.0,
            },
        ),
        (
            // Planck Current
            "PlanckCurrent",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 3.4789e+25,
                offset: 0.0,
            },
        ),
        (
            // Planck Current Density
            "PlanckCurrentDensity",
            UnitInfo {
                kind: "ElectricCurrentDensity",
                multiplier: 1.331774e+95,
                offset: 0.0,
            },
        ),
        (
            // Planck Density
            "PlanckDensity",
            UnitInfo {
                kind: "Density",
                multiplier: 5.155e+96,
                offset: 0.0,
            },
        ),
        (
            // Planck Energy
            "PlanckEnergy",
            UnitInfo {
                kind: "Energy",
                multiplier: 1956100000.0,
                offset: 0.0,
            },
        ),
        (
            // Planck Force
            "PlanckForce",
            UnitInfo {
                kind: "Force",
                multiplier: 1.21027e+44,
                offset: 0.0,
            },
        ),
        (
            // Planck Frequency
            "PlanckFrequency",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1.85487e+43,
                offset: 0.0,
            },
        ),
        (
            // Planck Angular Frequency
            "PlanckFrequency_Ang",
            UnitInfo {
                kind: "AngularFrequency",
                multiplier: 1.8548e+43,
                offset: 0.0,
            },
        ),
        (
            // Planck Impedance
            "PlanckImpedance",
            UnitInfo {
                kind: "Resistance",
                multiplier: 29.9792458,
                offset: 0.0,
            },
        ),
        (
            // Planck Length
            "PlanckLength",
            UnitInfo {
                kind: "Length",
                multiplier: 1.616252e-35,
                offset: 0.0,
            },
        ),
        (
            // Planck Mass
            "PlanckMass",
            UnitInfo {
                kind: "Mass",
                multiplier: 2.17644e-08,
                offset: 0.0,
            },
        ),
        (
            // Planck Momentum
            "PlanckMomentum",
            UnitInfo {
                kind: "LinearMomentum",
                multiplier: 6.52485,
                offset: 0.0,
            },
        ),
        (
            // Planck Power
            "PlanckPower",
            UnitInfo {
                kind: "Power",
                multiplier: 3.62831e+52,
                offset: 0.0,
            },
        ),
        (
            // Planck Pressure
            "PlanckPressure",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 4.63309e+113,
                offset: 0.0,
            },
        ),
        (
            // Plancktemperature
            "PlanckTemperature",
            UnitInfo {
                kind: "Temperature",
                multiplier: 1.416784e+32,
                offset: 0.0,
            },
        ),
        (
            // Planck Time
            "PlanckTime",
            UnitInfo {
                kind: "Time",
                multiplier: 5.39124e-49,
                offset: 0.0,
            },
        ),
        (
            // Planck Volt
            "PlanckVolt",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1.04295e+27,
                offset: 0.0,
            },
        ),
        (
            // Planck Volume
            "PlanckVolume",
            UnitInfo {
                kind: "Volume",
                multiplier: 4.22419e-105,
                offset: 0.0,
            },
        ),
        (
            // Quart (UK)
            "QT_UK",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.0011365225,
                offset: 0.0,
            },
        ),
        (
            // Quart (UK) per Day
            "QT_UK-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.3154195601851852e-08,
                offset: 0.0,
            },
        ),
        (
            // Quart (UK) per Hour
            "QT_UK-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 3.1570069444444446e-07,
                offset: 0.0,
            },
        ),
        (
            // Quart (UK) per Minute
            "QT_UK-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.8942041666666668e-05,
                offset: 0.0,
            },
        ),
        (
            // Quart (UK) per Second
            "QT_UK-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0011365225,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Quart
            "QT_US",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.000946352946,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Quart per Day
            "QT_US-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.0953159097222222e-08,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Quart per Hour
            "QT_US-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 2.6287581833333336e-07,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Quart per Minute
            "QT_US-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 1.57725491e-05,
                offset: 0.0,
            },
        ),
        (
            // Us Liquid Quart per Second
            "QT_US-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.000946352946,
                offset: 0.0,
            },
        ),
        (
            // Us Dry Quart
            "QT_US_DRY",
            UnitInfo {
                kind: "DryVolume",
                multiplier: 0.001101220942715,
                offset: 0.0,
            },
        ),
        (
            // Quad
            "QUAD",
            UnitInfo {
                kind: "Energy",
                multiplier: 1.055e+18,
                offset: 0.0,
            },
        ),
        (
            // Quarter (UK)
            "Quarter_UK",
            UnitInfo {
                kind: "Mass",
                multiplier: 12.70058636,
                offset: 0.0,
            },
        ),
        (
            // Roentgen
            "R",
            UnitInfo {
                kind: "ElectricChargePerMass",
                multiplier: 0.000258,
                offset: 0.0,
            },
        ),
        (
            // Roentgen per Second
            "R-PER-SEC",
            UnitInfo {
                kind: "ExposureRate",
                multiplier: 0.000258,
                offset: 0.0,
            },
        ),
        (
            // Radian
            "RAD",
            UnitInfo {
                kind: "Angle",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Radian Square Metre per Kilogram
            "RAD-M2-PER-KiloGM",
            UnitInfo {
                kind: "SpecificOpticalRotatoryPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Radian Square Metre per Mole
            "RAD-M2-PER-MOL",
            UnitInfo {
                kind: "MolarOpticalRotatoryPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Radian per Hour
            "RAD-PER-HR",
            UnitInfo {
                kind: "AngularFrequency",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Radian per Metre
            "RAD-PER-M",
            UnitInfo {
                kind: "AngularWavenumber",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Radian per Minute
            "RAD-PER-MIN",
            UnitInfo {
                kind: "AngularFrequency",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Radian per Second
            "RAD-PER-SEC",
            UnitInfo {
                kind: "AngularFrequency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Radian per Square Second
            "RAD-PER-SEC2",
            UnitInfo {
                kind: "AngularAcceleration",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Rad
            "RAD_R",
            UnitInfo {
                kind: "AbsorbedDose",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Rayl
            "RAYL",
            UnitInfo {
                kind: "SpecificAcousticImpedance",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Rayl_MKS
            "RAYL_MKS",
            UnitInfo {
                kind: "SpecificAcousticImpedance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Rem
            "REM",
            UnitInfo {
                kind: "DoseEquivalent",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Rem per Second
            "REM-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Revolution
            "REV",
            UnitInfo {
                kind: "Angle",
                multiplier: 6.283185307179586,
                offset: 0.0,
            },
        ),
        (
            // Revolution per Hour
            "REV-PER-HR",
            UnitInfo {
                kind: "RotationalFrequency",
                multiplier: 0.0017453292519943296,
                offset: 0.0,
            },
        ),
        (
            // Revolution per Minute
            "REV-PER-MIN",
            UnitInfo {
                kind: "RotationalFrequency",
                multiplier: 0.10471975511965978,
                offset: 0.0,
            },
        ),
        (
            // Revolution per Minute Second
            "REV-PER-MIN-SEC",
            UnitInfo {
                kind: "AngularAcceleration",
                multiplier: 0.10471975511965978,
                offset: 0.0,
            },
        ),
        (
            // Revolution per Second
            "REV-PER-SEC",
            UnitInfo {
                kind: "RotationalFrequency",
                multiplier: 6.283185307179586,
                offset: 0.0,
            },
        ),
        (
            // Revolution per Square Second
            "REV-PER-SEC2",
            UnitInfo {
                kind: "AngularAcceleration",
                multiplier: 6.283185307179586,
                offset: 0.0,
            },
        ),
        (
            // Rhe
            "RHE",
            UnitInfo {
                kind: "Fluidity",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Rod
            "ROD",
            UnitInfo {
                kind: "Length",
                multiplier: 5.02921,
                offset: 0.0,
            },
        ),
        (
            // Reads per Kilobase
            "RPK",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Register Ton
            "RT",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Roentgen Equivalent Man
            "R_man",
            UnitInfo {
                kind: "DoseEquivalent",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Richter Magnitude
            "RichterMagnitude",
            UnitInfo {
                kind: "EarthquakeMagnitude",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Siemens
            "S",
            UnitInfo {
                kind: "Admittance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Siemens Square Metre per Mole
            "S-M2-PER-MOL",
            UnitInfo {
                kind: "MolarConductivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Siemens per Centimetre
            "S-PER-CentiM",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Siemens per Metre
            "S-PER-M",
            UnitInfo {
                kind: "Conductivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Sample
            "SAMPLE",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Sample per Second
            "SAMPLE-PER-SEC",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Foot
            "SCF",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 1.1981,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Foot per Hour
            "SCF-PER-HR",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 0.00033280555555555553,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Foot per Minute
            "SCF-PER-MIN",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 0.019968333333333334,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Metre
            "SCM",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 42.3105,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Metre per Hour
            "SCM-PER-HR",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 0.011752916666666667,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Metre per Minute
            "SCM-PER-MIN",
            UnitInfo {
                kind: "MolarFlowRate",
                multiplier: 0.705175,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Metre
            "SCM_1ATM_0DEG_C",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 42.3105,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Metre
            "SCM_1ATM_15DEG_C_ISO",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Standard Cubic Metre
            "SCM_1ATM_15DEG_C_NL",
            UnitInfo {
                kind: "AmountOfSubstance",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Second
            "SEC",
            UnitInfo {
                kind: "Time",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Second Square Foot
            "SEC-FT2",
            UnitInfo {
                kind: "AreaTime",
                multiplier: 0.09290304,
                offset: 0.0,
            },
        ),
        (
            // Second per Kilogram
            "SEC-PER-KiloGM",
            UnitInfo {
                kind: "EinsteinCoefficients",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Second per Cubic Metre Radian
            "SEC-PER-M3-RAD",
            UnitInfo {
                kind: "SpectralDensityOfVibrationalModes",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Second per Number
            "SEC-PER-NUM",
            UnitInfo {
                kind: "TimePerCount",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Second per Radian Cubic Metre
            "SEC-PER-RAD-M3",
            UnitInfo {
                kind: "VibrationalDensityOfStates",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Second
            "SEC2",
            UnitInfo {
                kind: "SquareTime",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Shake
            "SH",
            UnitInfo {
                kind: "Time",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Shannon
            "SHANNON",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 0.6931471805599453,
                offset: 0.0,
            },
        ),
        (
            // Shannon per Second
            "SHANNON-PER-SEC",
            UnitInfo {
                kind: "InformationFlowRate",
                multiplier: 0.6931471805599453,
                offset: 0.0,
            },
        ),
        (
            // Slug
            "SLUG",
            UnitInfo {
                kind: "Mass",
                multiplier: 14.5939035919985,
                offset: 0.0,
            },
        ),
        (
            // Slug per Day
            "SLUG-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.00016891092120368634,
                offset: 0.0,
            },
        ),
        (
            // Slug per Foot
            "SLUG-PER-FT",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 47.880261128604005,
                offset: 0.0,
            },
        ),
        (
            // Slug per Foot Second
            "SLUG-PER-FT-SEC",
            UnitInfo {
                kind: "DynamicViscosity",
                multiplier: 47.880261128604005,
                offset: 0.0,
            },
        ),
        (
            // Slug per Square Foot
            "SLUG-PER-FT2",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 157.08747089437009,
                offset: 0.0,
            },
        ),
        (
            // Slug per Cubic Foot
            "SLUG-PER-FT3",
            UnitInfo {
                kind: "Density",
                multiplier: 515.3788415169622,
                offset: 0.0,
            },
        ),
        (
            // Slug per Hour
            "SLUG-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.004053862108888472,
                offset: 0.0,
            },
        ),
        (
            // Slug per Minute
            "SLUG-PER-MIN",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.24323172653330832,
                offset: 0.0,
            },
        ),
        (
            // Slug per Second
            "SLUG-PER-SEC",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 14.5939035919985,
                offset: 0.0,
            },
        ),
        (
            // Sun Protection Factor
            "SPF",
            UnitInfo {
                kind: "SunProtectionFactorOfAProduct",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Steradian
            "SR",
            UnitInfo {
                kind: "SolidAngle",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Stokes
            "ST",
            UnitInfo {
                kind: "KinematicViscosity",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Standard
            "STANDARD",
            UnitInfo {
                kind: "Volume",
                multiplier: 4.672,
                offset: 0.0,
            },
        ),
        (
            // Stilb
            "STILB",
            UnitInfo {
                kind: "Luminance",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Stere
            "STR",
            UnitInfo {
                kind: "Volume",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Electric Susceptibility Unit
            "SUSCEPTIBILITY_ELEC",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Magnetic Susceptibility Unit
            "SUSCEPTIBILITY_MAG",
            UnitInfo {
                kind: "Dimensionless",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Sievert
            "SV",
            UnitInfo {
                kind: "DoseEquivalent",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Sievert per Hour
            "SV-PER-HR",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 0.0002777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Sievert per Minute
            "SV-PER-MIN",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 0.016666666666666666,
                offset: 0.0,
            },
        ),
        (
            // Sievert per Second
            "SV-PER-SEC",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Absiemen
            "S_Ab",
            UnitInfo {
                kind: "ElectricConductivity",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Statsiemens
            "S_Stat",
            UnitInfo {
                kind: "ElectricConductivity",
                multiplier: 1.1126500561e-12,
                offset: 0.0,
            },
        ),
        (
            // Solar Mass
            "SolarMass",
            UnitInfo {
                kind: "Mass",
                multiplier: 1.988435e+30,
                offset: 0.0,
            },
        ),
        (
            // Speed of Light
            "SpeedOfLight",
            UnitInfo {
                kind: "SpeedOfLight",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Stone (UK)
            "Stone_UK",
            UnitInfo {
                kind: "Mass",
                multiplier: 6.35029318,
                offset: 0.0,
            },
        ),
        (
            // Tesla
            "T",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Tesla Metre
            "T-M",
            UnitInfo {
                kind: "MagneticFluxPerLength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Tesla Second
            "T-SEC",
            UnitInfo {
                kind: "MassPerElectricCharge",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Tablespoon
            "TBSP",
            UnitInfo {
                kind: "Volume",
                multiplier: 1.47867656e-05,
                offset: 0.0,
            },
        ),
        (
            // Ten
            "TEN",
            UnitInfo {
                kind: "Count",
                multiplier: 10.0,
                offset: 0.0,
            },
        ),
        (
            // Tex
            "TEX",
            UnitInfo {
                kind: "MassPerLength",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Therm (ec)
            "THERM_EC",
            UnitInfo {
                kind: "Energy",
                multiplier: 1055055852.62,
                offset: 0.0,
            },
        ),
        (
            // Therm (u.s.)
            "THERM_US",
            UnitInfo {
                kind: "Energy",
                multiplier: 105480400.0,
                offset: 0.0,
            },
        ),
        (
            // Thm_eec
            "THM_EEC",
            UnitInfo {
                kind: "Energy",
                multiplier: 1055055852.62,
                offset: 0.0,
            },
        ),
        (
            // Therm Us
            "THM_US",
            UnitInfo {
                kind: "Energy",
                multiplier: 105480400.0,
                offset: 0.0,
            },
        ),
        (
            // Therm Us per Hour
            "THM_US-PER-HR",
            UnitInfo {
                kind: "Power",
                multiplier: 29300.11111111111,
                offset: 0.0,
            },
        ),
        (
            // Thousand
            "THOUSAND",
            UnitInfo {
                kind: "Count",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Ton of Oil Equivalent
            "TOE",
            UnitInfo {
                kind: "Energy",
                multiplier: 41868000000.0,
                offset: 0.0,
            },
        ),
        (
            // Ton
            "TON",
            UnitInfo {
                kind: "Mass",
                multiplier: 907.18474,
                offset: 0.0,
            },
        ),
        (
            // Tonne
            "TONNE",
            UnitInfo {
                kind: "Mass",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Day
            "TONNE-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.011574074074074073,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Hectare
            "TONNE-PER-HA",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Hectare Year
            "TONNE-PER-HA-YR",
            UnitInfo {
                kind: "MassPerAreaTime",
                multiplier: 3.168808781402895e-09,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Hour
            "TONNE-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.2777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Cubic Metre
            "TONNE-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Cubic Metre Bar
            "TONNE-PER-M3-BAR",
            UnitInfo {
                kind: "MassPerEnergy",
                multiplier: 0.01,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Minute
            "TONNE-PER-MIN",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 16.666666666666668,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Month
            "TONNE-PER-MO",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.0003919350772901616,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Second
            "TONNE-PER-SEC",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Tonne per Year
            "TONNE-PER-YR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 3.168808781402895e-05,
                offset: 0.0,
            },
        ),
        (
            // Assay Ton
            "TON_Assay",
            UnitInfo {
                kind: "Mass",
                multiplier: 0.02916667,
                offset: 0.0,
            },
        ),
        (
            // Ton of Refrigeration
            "TON_FG",
            UnitInfo {
                kind: "Power",
                multiplier: 3516.853,
                offset: 0.0,
            },
        ),
        (
            // Ton of Refrigeration Hour
            "TON_FG-HR",
            UnitInfo {
                kind: "ThermalEnergy",
                multiplier: 12660670.8,
                offset: 0.0,
            },
        ),
        (
            // Ton Force (us Short)
            "TON_F_US",
            UnitInfo {
                kind: "Force",
                multiplier: 8896.443230521,
                offset: 0.0,
            },
        ),
        (
            // Long Ton
            "TON_LONG",
            UnitInfo {
                kind: "Mass",
                multiplier: 1016.0469088,
                offset: 0.0,
            },
        ),
        (
            // Long Ton per Cubic Yard
            "TON_LONG-PER-YD3",
            UnitInfo {
                kind: "Density",
                multiplier: 1328.9391836174339,
                offset: 0.0,
            },
        ),
        (
            // Metric Ton
            "TON_Metric",
            UnitInfo {
                kind: "Mass",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Metric Ton per Day
            "TON_Metric-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.011574074074074073,
                offset: 0.0,
            },
        ),
        (
            // Metric Ton per Hectare
            "TON_Metric-PER-HA",
            UnitInfo {
                kind: "MassPerArea",
                multiplier: 0.1,
                offset: 0.0,
            },
        ),
        (
            // Metric Ton per Hour
            "TON_Metric-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.2777777777777778,
                offset: 0.0,
            },
        ),
        (
            // Metric Ton per Cubic Metre
            "TON_Metric-PER-M3",
            UnitInfo {
                kind: "Density",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Metric Ton per Minute
            "TON_Metric-PER-MIN",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 16.666666666666668,
                offset: 0.0,
            },
        ),
        (
            // Metric Ton per Second
            "TON_Metric-PER-SEC",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Ton, Register
            "TON_Register",
            UnitInfo {
                kind: "Volume",
                multiplier: 2.83,
                offset: 0.0,
            },
        ),
        (
            // Ton (uk Shipping)
            "TON_SHIPPING_UK",
            UnitInfo {
                kind: "Volume",
                multiplier: 1.1326,
                offset: 0.0,
            },
        ),
        (
            // Ton (us Shipping)
            "TON_SHIPPING_US",
            UnitInfo {
                kind: "Volume",
                multiplier: 1.1326,
                offset: 0.0,
            },
        ),
        (
            // Short Ton
            "TON_SHORT",
            UnitInfo {
                kind: "Mass",
                multiplier: 907.18474,
                offset: 0.0,
            },
        ),
        (
            // Short Ton per Hour
            "TON_SHORT-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.2519957611111111,
                offset: 0.0,
            },
        ),
        (
            // Short Ton per Cubic Yard
            "TON_SHORT-PER-YD3",
            UnitInfo {
                kind: "Density",
                multiplier: 1186.552842515566,
                offset: 0.0,
            },
        ),
        (
            // Ton (UK)
            "TON_UK",
            UnitInfo {
                kind: "Mass",
                multiplier: 1016.0469088,
                offset: 0.0,
            },
        ),
        (
            // Ton (UK) per Day
            "TON_UK-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.011759802185185185,
                offset: 0.0,
            },
        ),
        (
            // Ton (UK) per Hour
            "TON_UK-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.2822352524444444,
                offset: 0.0,
            },
        ),
        (
            // Ton (UK) per Cubic Yard
            "TON_UK-PER-YD3",
            UnitInfo {
                kind: "Density",
                multiplier: 1328.9391836174339,
                offset: 0.0,
            },
        ),
        (
            // Ton (US)
            "TON_US",
            UnitInfo {
                kind: "Mass",
                multiplier: 907.18474,
                offset: 0.0,
            },
        ),
        (
            // Ton (US) per Day
            "TON_US-PER-DAY",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.01049982337962963,
                offset: 0.0,
            },
        ),
        (
            // Ton (US) per Hour
            "TON_US-PER-HR",
            UnitInfo {
                kind: "MassFlowRate",
                multiplier: 0.2519957611111111,
                offset: 0.0,
            },
        ),
        (
            // Ton (US) per Cubic Yard
            "TON_US-PER-YD3",
            UnitInfo {
                kind: "Density",
                multiplier: 1186.552842515566,
                offset: 0.0,
            },
        ),
        (
            // Torr
            "TORR",
            UnitInfo {
                kind: "ForcePerArea",
                multiplier: 133.322,
                offset: 0.0,
            },
        ),
        (
            // Torr per Metre
            "TORR-PER-M",
            UnitInfo {
                kind: "PressureGradient",
                multiplier: 133.322,
                offset: 0.0,
            },
        ),
        (
            // Teaspoon
            "TSP",
            UnitInfo {
                kind: "Volume",
                multiplier: 4.92892187e-06,
                offset: 0.0,
            },
        ),
        (
            // Abtesla
            "T_Ab",
            UnitInfo {
                kind: "MagneticFluxDensity",
                multiplier: 0.0001,
                offset: 0.0,
            },
        ),
        (
            // Tebibit
            "TebiBIT",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 762123384785.8104,
                offset: 0.0,
            },
        ),
        (
            // Tebibit per Metre
            "TebiBIT-PER-M",
            UnitInfo {
                kind: "LinearBitDensity",
                multiplier: 762123384785.8104,
                offset: 0.0,
            },
        ),
        (
            // Tebibit per Square Metre
            "TebiBIT-PER-M2",
            UnitInfo {
                kind: "AreaBitDensity",
                multiplier: 762123384785.8104,
                offset: 0.0,
            },
        ),
        (
            // Tebibit per Cubic Metre
            "TebiBIT-PER-M3",
            UnitInfo {
                kind: "VolumetricBitDensity",
                multiplier: 762123384785.8104,
                offset: 0.0,
            },
        ),
        (
            // Tebibyte
            "TebiBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 6096987078286.483,
                offset: 0.0,
            },
        ),
        (
            // Teraampere
            "TeraA",
            UnitInfo {
                kind: "ElectricCurrent",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Terabit
            "TeraBIT",
            UnitInfo {
                kind: "DatasetOfBits",
                multiplier: 693147180559.9453,
                offset: 0.0,
            },
        ),
        (
            // Terabit per Second
            "TeraBIT-PER-SEC",
            UnitInfo {
                kind: "DataRate",
                multiplier: 693147180559.9453,
                offset: 0.0,
            },
        ),
        (
            // Terabecquerel
            "TeraBQ",
            UnitInfo {
                kind: "Activity",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Terabyte
            "TeraBYTE",
            UnitInfo {
                kind: "InformationEntropy",
                multiplier: 5545177444479.5625,
                offset: 0.0,
            },
        ),
        (
            // Teracoulomb
            "TeraC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Tera Floating Point Operations per Second
            "TeraFLOPS",
            UnitInfo {
                kind: "FloatingPointCalculationCapability",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Terahertz
            "TeraHZ",
            UnitInfo {
                kind: "Frequency",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Terajoule
            "TeraJ",
            UnitInfo {
                kind: "Energy",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Terajoule per Second
            "TeraJ-PER-SEC",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Teraohm
            "TeraOHM",
            UnitInfo {
                kind: "Resistance",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Teravolt
            "TeraV",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Tera Volt Ampere
            "TeraVA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Tera Volt Ampere Reactive
            "TeraVAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Terawatt
            "TeraW",
            UnitInfo {
                kind: "Power",
                multiplier: 1000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Terawatt Hour
            "TeraW-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600000000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Terawatt Hour per Year
            "TeraW-HR-PER-YR",
            UnitInfo {
                kind: "Power",
                multiplier: 114077116.13050422,
                offset: 0.0,
            },
        ),
        (
            // Ton Energy
            "TonEnergy",
            UnitInfo {
                kind: "Energy",
                multiplier: 4184000000.0,
                offset: 0.0,
            },
        ),
        (
            // Unified Atomic Mass Unit
            "U",
            UnitInfo {
                kind: "Mass",
                multiplier: 1.66053878283e-27,
                offset: 0.0,
            },
        ),
        (
            // Unitless
            "UNITLESS",
            UnitInfo {
                kind: "Count",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Unit Pole
            "UnitPole",
            UnitInfo {
                kind: "MagneticFlux",
                multiplier: 1.256637e-07,
                offset: 0.0,
            },
        ),
        (
            // Volt
            "V",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt Square Inch per Pound Force
            "V-IN2-PER-LB_F",
            UnitInfo {
                kind: "HallCoefficient",
                multiplier: 0.00014503773122272686,
                offset: 0.0,
            },
        ),
        (
            // Volt Metre
            "V-M",
            UnitInfo {
                kind: "ElectricFlux",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt per Bar
            "V-PER-BAR",
            UnitInfo {
                kind: "HallCoefficient",
                multiplier: 1e-05,
                offset: 0.0,
            },
        ),
        (
            // Volt per Centimetre
            "V-PER-CentiM",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 100.0,
                offset: 0.0,
            },
        ),
        (
            // Volt per Inch
            "V-PER-IN",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 39.37007874015748,
                offset: 0.0,
            },
        ),
        (
            // Volt per Kelvin
            "V-PER-K",
            UnitInfo {
                kind: "SeebeckCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt per Metre
            "V-PER-M",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt per Square Metre
            "V-PER-M2",
            UnitInfo {
                kind: "EnergyPerAreaElectricCharge",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt per Microsecond
            "V-PER-MicroSEC",
            UnitInfo {
                kind: "PowerPerElectricCharge",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Volt per Millimetre
            "V-PER-MilliM",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Volt per Pascal
            "V-PER-PA",
            UnitInfo {
                kind: "HallCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt per Second
            "V-PER-SEC",
            UnitInfo {
                kind: "PowerPerElectricCharge",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt Second per Metre
            "V-SEC-PER-M",
            UnitInfo {
                kind: "MagneticVectorPotential",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Square Volt per Square Kelvin
            "V2-PER-K2",
            UnitInfo {
                kind: "LorenzCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt Ampere
            "VA",
            UnitInfo {
                kind: "ApparentPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt Ampere Hour
            "VA-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Volt Ampere per Kelvin
            "VA-PER-K",
            UnitInfo {
                kind: "ThermalConductance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt Ampere Reactive
            "VAR",
            UnitInfo {
                kind: "ReactivePower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Volt Ampere Reactive Hour
            "VAR-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Abvolt
            "V_Ab",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Abvolt per Centimetre
            "V_Ab-PER-CentiM",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 1e-06,
                offset: 0.0,
            },
        ),
        (
            // Abvolt Second
            "V_Ab-SEC",
            UnitInfo {
                kind: "MagneticFlux",
                multiplier: 1e-08,
                offset: 0.0,
            },
        ),
        (
            // Statvolt
            "V_Stat",
            UnitInfo {
                kind: "ElectricPotential",
                multiplier: 299.792458,
                offset: 0.0,
            },
        ),
        (
            // Statvolt Centimetre
            "V_Stat-CentiM",
            UnitInfo {
                kind: "ElectricFlux",
                multiplier: 2.99792458,
                offset: 0.0,
            },
        ),
        (
            // Statvolt per Centimetre
            "V_Stat-PER-CentiM",
            UnitInfo {
                kind: "ElectricFieldStrength",
                multiplier: 29979.2458,
                offset: 0.0,
            },
        ),
        (
            // Watt
            "W",
            UnitInfo {
                kind: "Power",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt Hour
            "W-HR",
            UnitInfo {
                kind: "Energy",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Watt Hour per Kilogram
            "W-HR-PER-KiloGM",
            UnitInfo {
                kind: "SpecificEnergy",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Watt Hour per Square Metre
            "W-HR-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Watt Hour per Cubic Metre
            "W-HR-PER-M3",
            UnitInfo {
                kind: "EnergyDensity",
                multiplier: 3600.0,
                offset: 0.0,
            },
        ),
        (
            // Watt Square Metre
            "W-M2",
            UnitInfo {
                kind: "PowerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt Square Metre per Steradian
            "W-M2-PER-SR",
            UnitInfo {
                kind: "PowerAreaPerSolidAngle",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Centimetre
            "W-PER-CentiM2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 10000.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Foot
            "W-PER-FT2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 10.763910416709722,
                offset: 0.0,
            },
        ),
        (
            // Watt per Gram
            "W-PER-GM",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Inch
            "W-PER-IN2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 1550.0031000062,
                offset: 0.0,
            },
        ),
        (
            // Watt per Kelvin
            "W-PER-K",
            UnitInfo {
                kind: "ThermalConductance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Kilogram
            "W-PER-KiloGM",
            UnitInfo {
                kind: "AbsorbedDoseRate",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Metre
            "W-PER-M",
            UnitInfo {
                kind: "LineicPower",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Metre Degree Celsius
            "W-PER-M-DEG_C",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Metre Kelvin
            "W-PER-M-K",
            UnitInfo {
                kind: "ThermalConductivity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Metre
            "W-PER-M2",
            UnitInfo {
                kind: "PowerPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Metre Kelvin
            "W-PER-M2-K",
            UnitInfo {
                kind: "CoefficientOfHeatTransfer",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Metre Quartic Kelvin
            "W-PER-M2-K4",
            UnitInfo {
                kind: "PowerPerAreaQuarticTemperature",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Metre Micrometre
            "W-PER-M2-MicroM",
            UnitInfo {
                kind: "SpectralRadiance",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Metre Micrometre Steradian
            "W-PER-M2-MicroM-SR",
            UnitInfo {
                kind: "SpectralRadiance",
                multiplier: 1000000.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Metre Nanometre Steradian
            "W-PER-M2-NanoM-SR",
            UnitInfo {
                kind: "SpectralRadiance",
                multiplier: 1000000000.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Metre Pascal
            "W-PER-M2-PA",
            UnitInfo {
                kind: "EvaporativeHeatTransferCoefficient",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Square Metre Steradian
            "W-PER-M2-SR",
            UnitInfo {
                kind: "Radiance",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Cubic Metre
            "W-PER-M3",
            UnitInfo {
                kind: "PowerDensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt per Steradian
            "W-PER-SR",
            UnitInfo {
                kind: "RadiantIntensity",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt Second
            "W-SEC",
            UnitInfo {
                kind: "Energy",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Watt Second per Square Metre
            "W-SEC-PER-M2",
            UnitInfo {
                kind: "EnergyPerArea",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Weber
            "WB",
            UnitInfo {
                kind: "MagneticFlux",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Weber Metre
            "WB-M",
            UnitInfo {
                kind: "MagneticDipoleMoment",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Weber per Metre
            "WB-PER-M",
            UnitInfo {
                kind: "MagneticVectorPotential",
                multiplier: 1.0,
                offset: 0.0,
            },
        ),
        (
            // Weber per Millimetre
            "WB-PER-MilliM",
            UnitInfo {
                kind: "MagneticVectorPotential",
                multiplier: 1000.0,
                offset: 0.0,
            },
        ),
        (
            // Week
            "WK",
            UnitInfo {
                kind: "Time",
                multiplier: 604800.0,
                offset: 0.0,
            },
        ),
        (
            // Week per Number
            "WK-PER-NUM",
            UnitInfo {
                kind: "TimePerCount",
                multiplier: 604800.0,
                offset: 0.0,
            },
        ),
        (
            // Yard
            "YD",
            UnitInfo {
                kind: "Length",
                multiplier: 0.9144,
                offset: 0.0,
            },
        ),
        (
            // Yard per Degree Fahrenheit
            "YD-PER-DEG_F",
            UnitInfo {
                kind: "LinearThermalExpansion",
                multiplier: 1.64592,
                offset: 0.0,
            },
        ),
        (
            // Yard per Hour
            "YD-PER-HR",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.000254,
                offset: 0.0,
            },
        ),
        (
            // Yard per Minute
            "YD-PER-MIN",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.01524,
                offset: 0.0,
            },
        ),
        (
            // Yard per Second
            "YD-PER-SEC",
            UnitInfo {
                kind: "LinearVelocity",
                multiplier: 0.9144,
                offset: 0.0,
            },
        ),
        (
            // Yard per Square Second
            "YD-PER-SEC2",
            UnitInfo {
                kind: "Acceleration",
                multiplier: 0.9144,
                offset: 0.0,
            },
        ),
        (
            // Square Yard
            "YD2",
            UnitInfo {
                kind: "Area",
                multiplier: 0.83612736,
                offset: 0.0,
            },
        ),
        (
            // Cubic Yard
            "YD3",
            UnitInfo {
                kind: "Volume",
                multiplier: 0.764554857984,
                offset: 0.0,
            },
        ),
        (
            // Cubic Yard per Day
            "YD3-PER-DAY",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 8.84901456e-06,
                offset: 0.0,
            },
        ),
        (
            // Cubic Yard per Degree Fahrenheit
            "YD3-PER-DEG_F",
            UnitInfo {
                kind: "VolumeThermalExpansion",
                multiplier: 1.3761987443712,
                offset: 0.0,
            },
        ),
        (
            // Cubic Yard per Hour
            "YD3-PER-HR",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.00021237634944,
                offset: 0.0,
            },
        ),
        (
            // Cubic Yard per Minute
            "YD3-PER-MIN",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.0127425809664,
                offset: 0.0,
            },
        ),
        (
            // Cubic Yard per Second
            "YD3-PER-SEC",
            UnitInfo {
                kind: "VolumeFlowRate",
                multiplier: 0.764554857984,
                offset: 0.0,
            },
        ),
        (
            // Year
            "YR",
            UnitInfo {
                kind: "Time",
                multiplier: 31557600.0,
                offset: 0.0,
            },
        ),
        (
            // Year per Number
            "YR-PER-NUM",
            UnitInfo {
                kind: "TimePerCount",
                multiplier: 31557600.0,
                offset: 0.0,
            },
        ),
        (
            // Common Year
            "YR_Common",
            UnitInfo {
                kind: "Time",
                multiplier: 31536000.0,
                offset: 0.0,
            },
        ),
        (
            // Metrology Year
            "YR_Metrology",
            UnitInfo {
                kind: "Time",
                multiplier: 31557600.0,
                offset: 0.0,
            },
        ),
        (
            // Sidereal Year
            "YR_Sidereal",
            UnitInfo {
                kind: "Time",
                multiplier: 31558149.7632,
                offset: 0.0,
            },
        ),
        (
            // Tropical Year
            "YR_TROPICAL",
            UnitInfo {
                kind: "Time",
                multiplier: 31556925.216,
                offset: 0.0,
            },
        ),
        (
            // Yoctocoulomb
            "YoctoC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e-24,
                offset: 0.0,
            },
        ),
        (
            // Yottacoulomb
            "YottaC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e+24,
                offset: 0.0,
            },
        ),
        (
            // Atomic-number
            "Z",
            UnitInfo {
                kind: "AtomicNumber",
                multiplier: 0.0,
                offset: 0.0,
            },
        ),
        (
            // Inch
            "ZOLL",
            UnitInfo {
                kind: "Length",
                multiplier: 0.0254,
                offset: 0.0,
            },
        ),
        (
            // Zeptocoulomb
            "ZeptoC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e-21,
                offset: 0.0,
            },
        ),
        (
            // Zettacoulomb
            "ZettaC",
            UnitInfo {
                kind: "ElectricCharge",
                multiplier: 1e+21,
                offset: 0.0,
            },
        ),
    ])
});
