namespace hitachi_yutaki_2016_mqtt_gateway;

internal static class RegisterDefinitions
{
    public static readonly IReadOnlyList<RegisterDefinition> All = BuildAll();

    private static RegisterDefinition Enum(ushort addr, string name, RegisterGroup group, bool writable, params (int v, string s)[] values) =>
        new()
        {
            Address = addr, Name = name, Group = group, Kind = RegisterKind.Enum, IsWritable = writable,
            EnumValues = values.ToDictionary(x => x.v, x => x.s)
        };

    private static RegisterDefinition Analog(ushort addr, string name, RegisterGroup group, bool writable, double scale = 1.0, string? unit = null, bool signed = false) =>
        new() { Address = addr, Name = name, Group = group, Kind = RegisterKind.Analog, IsWritable = writable, Scale = scale, Unit = unit, IsSigned = signed };

    private static RegisterDefinition Bitmask(ushort addr, string name, RegisterGroup group, string prefix, params (int bit, string label)[] bits) =>
        new()
        {
            Address = addr, Name = name, Group = group, Kind = RegisterKind.Bitmask, IsWritable = false,
            BitmaskPrefix = prefix,
            BitNames = bits.ToDictionary(x => x.bit, x => x.label)
        };

    private static RegisterDefinition Raw(ushort addr, string name, RegisterGroup group) =>
        new() { Address = addr, Name = name, Group = group, Kind = RegisterKind.Raw, IsWritable = false };

    private static IReadOnlyList<RegisterDefinition> BuildAll() =>
    [
        // ── Control R/W (addresses 1000–1033) ──────────────────────────────────
        Enum(1000, "unit_run_stop",                         RegisterGroup.Unit,     true,  (0,"Stop"), (1,"Run"))
            with { Description = "Start or stop the entire heat pump unit" },
        Enum(1001, "unit_mode",                             RegisterGroup.Unit,     true,  (0,"Cool"), (1,"Heat"), (2,"Auto"))
            with { Description = "Operating mode: cooling, heating, or automatic selection" },
        Enum(1002, "circuit1_run_stop",                     RegisterGroup.Circuit1, true,  (0,"Stop"), (1,"Run"))
            with { Description = "Start or stop hydronic circuit 1" },
        Enum(1003, "circuit1_heat_otc",                     RegisterGroup.Circuit1, true,  (0,"No"), (1,"Points"), (2,"Gradient"), (3,"Fix"))
            with { Description = "Heating outdoor temperature compensation (OTC) mode for circuit 1: disabled, two-point curve, gradient curve, or fixed setpoint" },
        Enum(1004, "circuit1_cool_otc",                     RegisterGroup.Circuit1, true,  (0,"No"), (1,"Points"), (2,"Fix"))
            with { Description = "Cooling OTC mode for circuit 1: disabled, two-point curve, or fixed setpoint" },
        Analog(1005, "circuit1_water_heating_setpoint",     RegisterGroup.Circuit1, true,  unit: "°C")
            with { Description = "Target water outlet temperature for circuit 1 in heating mode (used when OTC is Fix or disabled)" },
        Analog(1006, "circuit1_water_cooling_setpoint",     RegisterGroup.Circuit1, true,  unit: "°C")
            with { Description = "Target water outlet temperature for circuit 1 in cooling mode" },
        Enum(1007, "circuit1_eco_mode",                     RegisterGroup.Circuit1, true,  (0,"ECO"), (1,"Comfort"))
            with { Description = "Energy-saving mode for circuit 1: ECO applies an offset to reduce consumption, Comfort maintains full setpoint" },
        Analog(1008, "circuit1_heat_eco_offset",            RegisterGroup.Circuit1, true)
            with { Description = "Temperature offset subtracted from the heating setpoint when ECO mode is active on circuit 1 (°C)" },
        Analog(1009, "circuit1_cool_eco_offset",            RegisterGroup.Circuit1, true)
            with { Description = "Temperature offset added to the cooling setpoint when ECO mode is active on circuit 1 (°C)" },
        Enum(1010, "circuit1_thermostat_available",         RegisterGroup.Circuit1, true,  (0,"Not Available"), (1,"Available"))
            with { Description = "Enables room thermostat control for circuit 1; when available, the thermostat setpoint governs the water temperature curve" },
        Analog(1011, "circuit1_thermostat_setpoint",        RegisterGroup.Circuit1, true,  0.1, "°C")
            with { Description = "Room temperature setpoint for circuit 1 thermostat (0.1 °C resolution); the unit adjusts water temperature to reach this room target" },
        Analog(1012, "circuit1_thermostat_room_temperature",RegisterGroup.Circuit1, true,  0.1, "°C")
            with { Description = "Room temperature feedback sent to the unit for circuit 1 thermostat control (0.1 °C resolution)" },
        Enum(1013, "circuit2_run_stop",                     RegisterGroup.Circuit2, true,  (0,"Stop"), (1,"Run"))
            with { Description = "Start or stop hydronic circuit 2" },
        Enum(1014, "circuit2_heat_otc",                     RegisterGroup.Circuit2, true,  (0,"No"), (1,"Points"), (2,"Gradient"), (3,"Fix"))
            with { Description = "Heating OTC mode for circuit 2" },
        Enum(1015, "circuit2_cool_otc",                     RegisterGroup.Circuit2, true,  (0,"No"), (1,"Points"), (2,"Fix"))
            with { Description = "Cooling OTC mode for circuit 2" },
        Analog(1016, "circuit2_water_heating_setpoint",     RegisterGroup.Circuit2, true,  unit: "°C")
            with { Description = "Target water outlet temperature for circuit 2 in heating mode" },
        Analog(1017, "circuit2_water_cooling_setpoint",     RegisterGroup.Circuit2, true,  unit: "°C")
            with { Description = "Target water outlet temperature for circuit 2 in cooling mode" },
        Enum(1018, "circuit2_eco_mode",                     RegisterGroup.Circuit2, true,  (0,"ECO"), (1,"Comfort"))
            with { Description = "Energy-saving mode for circuit 2" },
        Analog(1019, "circuit2_heat_eco_offset",            RegisterGroup.Circuit2, true)
            with { Description = "Heating setpoint ECO offset for circuit 2 (°C)" },
        Analog(1020, "circuit2_cool_eco_offset",            RegisterGroup.Circuit2, true)
            with { Description = "Cooling setpoint ECO offset for circuit 2 (°C)" },
        Enum(1021, "circuit2_thermostat_available",         RegisterGroup.Circuit2, true,  (0,"Not Available"), (1,"Available"))
            with { Description = "Enables room thermostat control for circuit 2" },
        Analog(1022, "circuit2_thermostat_setpoint",        RegisterGroup.Circuit2, true,  0.1, "°C")
            with { Description = "Room temperature setpoint for circuit 2 thermostat (0.1 °C resolution)" },
        Analog(1023, "circuit2_thermostat_room_temperature",RegisterGroup.Circuit2, true,  0.1, "°C")
            with { Description = "Room temperature feedback for circuit 2 thermostat control (0.1 °C resolution)" },
        Enum(1024, "dhwt_run_stop",                         RegisterGroup.Dhw,      true,  (0,"Stop"), (1,"Run"))
            with { Description = "Start or stop domestic hot water (DHW) tank heating" },
        Analog(1025, "dhwt_setpoint",                       RegisterGroup.Dhw,      true,  unit: "°C")
            with { Description = "Target DHW tank temperature setpoint" },
        Enum(1026, "dhw_boost",                             RegisterGroup.Dhw,      true,  (0,"No request"), (1,"Request"))
            with { Description = "Activates DHW boost: forces an immediate DHW heating cycle regardless of schedule or current tank temperature" },
        Enum(1027, "dhw_demand_mode",                       RegisterGroup.Dhw,      true,  (0,"Standard"), (1,"High demand"))
            with { Description = "DHW heating priority: Standard interleaves DHW with space heating, High demand prioritizes DHW" },
        Enum(1028, "swimming_pool_run_stop",                RegisterGroup.Pool,     true,  (0,"Stop"), (1,"Run"))
            with { Description = "Start or stop swimming pool water heating" },
        Analog(1029, "swimming_pool_setpoint",              RegisterGroup.Pool,     true,  unit: "°C")
            with { Description = "Target swimming pool water temperature setpoint" },
        Enum(1030, "anti_legionella_run",                   RegisterGroup.Pool,     true,  (0,"Stop"), (1,"Run"))
            with { Description = "Activates anti-Legionella thermal disinfection cycle (raises DHW to high temperature)" },
        Analog(1031, "anti_legionella_setpoint",            RegisterGroup.Pool,     true,  unit: "°C")
            with { Description = "Target temperature for anti-Legionella disinfection (typically ≥60 °C)" },
        Enum(1032, "block_menu",                            RegisterGroup.Unit,     true,  (0,"No"), (1,"Block"))
            with { Description = "Locks the local HMI/controller menu to prevent unauthorized changes from the unit panel" },
        Enum(1033, "bms_alarm",                             RegisterGroup.Unit,     true,  (0,"No Alarm"), (1,"Alarm"))
            with { Description = "BMS-triggered alarm input: setting to Alarm forces the unit into alarm state from the building management system" },

        // ── Status R (addresses 1050–1098) ─────────────────────────────────────
        Enum(1050, "status_unit_run_stop",                          RegisterGroup.Unit,     false, (0,"Stop"), (1,"Run"))
            with { Description = "Actual run/stop state of the unit (may differ from command if unit is in protection mode)" },
        Enum(1051, "status_unit_mode",                              RegisterGroup.Unit,     false, (0,"Cool"), (1,"Heat"))
            with { Description = "Active operating mode currently executed by the unit" },
        Enum(1052, "status_circuit1_run_stop",                      RegisterGroup.Circuit1, false, (0,"Stop"), (1,"Run"))
            with { Description = "Actual run/stop state of circuit 1" },
        Enum(1053, "status_circuit1_heat_otc",                      RegisterGroup.Circuit1, false, (0,"No"), (1,"Points"), (2,"Gradient"), (3,"Fix"))
            with { Description = "Active heating OTC mode for circuit 1 as applied by the unit" },
        Enum(1054, "status_circuit1_cool_otc",                      RegisterGroup.Circuit1, false, (0,"No"), (1,"Points"), (2,"Fix"))
            with { Description = "Active cooling OTC mode for circuit 1 as applied by the unit" },
        Analog(1055, "status_circuit1_water_heating_setpoint",      RegisterGroup.Circuit1, false, unit: "°C")
            with { Description = "Effective water heating setpoint for circuit 1 after OTC curve calculation" },
        Analog(1056, "status_circuit1_water_cooling_setpoint",      RegisterGroup.Circuit1, false, unit: "°C")
            with { Description = "Effective water cooling setpoint for circuit 1 after OTC curve calculation" },
        Enum(1057, "status_circuit1_eco_mode",                      RegisterGroup.Circuit1, false, (0,"ECO"), (1,"Comfort"))
            with { Description = "Active ECO/Comfort mode for circuit 1" },
        Analog(1058, "status_circuit1_heat_eco_offset",             RegisterGroup.Circuit1, false)
            with { Description = "Active heating ECO offset applied to circuit 1 setpoint (°C)" },
        Analog(1059, "status_circuit1_cool_eco_offset",             RegisterGroup.Circuit1, false)
            with { Description = "Active cooling ECO offset applied to circuit 1 setpoint (°C)" },
        Analog(1060, "status_circuit1_thermostat_setpoint",         RegisterGroup.Circuit1, false, 0.1, "°C")
            with { Description = "Active room temperature setpoint used by the circuit 1 thermostat (0.1 °C resolution)" },
        Analog(1061, "status_circuit1_thermostat_room_temperature",  RegisterGroup.Circuit1, false, 0.1, "°C")
            with { Description = "Room temperature currently reported by the wired room thermostat on circuit 1 (0.1 °C resolution)" },
        Analog(1062, "status_circuit1_wireless_setpoint",           RegisterGroup.Circuit1, false, 0.1, "°C")
            with { Description = "Room temperature setpoint received from the wireless interface for circuit 1 (0.1 °C resolution)" },
        Analog(1063, "status_circuit1_wireless_room_temperature",    RegisterGroup.Circuit1, false, 0.1, "°C")
            with { Description = "Room temperature received from the wireless sensor for circuit 1 (0.1 °C resolution)" },
        Enum(1064, "status_circuit2_run_stop",                      RegisterGroup.Circuit2, false, (0,"Stop"), (1,"Run"))
            with { Description = "Actual run/stop state of circuit 2" },
        Enum(1065, "status_circuit2_heat_otc",                      RegisterGroup.Circuit2, false, (0,"No"), (1,"Points"), (2,"Gradient"), (3,"Fix"))
            with { Description = "Active heating OTC mode for circuit 2" },
        Enum(1066, "status_circuit2_cool_otc",                      RegisterGroup.Circuit2, false, (0,"No"), (1,"Points"), (2,"Fix"))
            with { Description = "Active cooling OTC mode for circuit 2" },
        Analog(1067, "status_circuit2_water_heating_setpoint",      RegisterGroup.Circuit2, false, unit: "°C")
            with { Description = "Effective water heating setpoint for circuit 2 after OTC curve calculation" },
        Analog(1068, "status_circuit2_water_cooling_setpoint",      RegisterGroup.Circuit2, false, unit: "°C")
            with { Description = "Effective water cooling setpoint for circuit 2 after OTC curve calculation" },
        Enum(1069, "status_circuit2_eco_mode",                      RegisterGroup.Circuit2, false, (0,"ECO"), (1,"Comfort"))
            with { Description = "Active ECO/Comfort mode for circuit 2" },
        Analog(1070, "status_circuit2_heat_eco_offset",             RegisterGroup.Circuit2, false)
            with { Description = "Active heating ECO offset applied to circuit 2 setpoint (°C)" },
        Analog(1071, "status_circuit2_cool_eco_offset",             RegisterGroup.Circuit2, false)
            with { Description = "Active cooling ECO offset applied to circuit 2 setpoint (°C)" },
        Analog(1072, "status_circuit2_thermostat_setpoint",         RegisterGroup.Circuit2, false, 0.1, "°C")
            with { Description = "Active room temperature setpoint used by the circuit 2 thermostat (0.1 °C resolution)" },
        Analog(1073, "status_circuit2_thermostat_room_temperature",  RegisterGroup.Circuit2, false, 0.1, "°C")
            with { Description = "Room temperature currently reported by the wired room thermostat on circuit 2 (0.1 °C resolution)" },
        Analog(1074, "status_circuit2_wireless_setpoint",           RegisterGroup.Circuit2, false, 0.1, "°C")
            with { Description = "Room temperature setpoint received from the wireless interface for circuit 2 (0.1 °C resolution)" },
        Analog(1075, "status_circuit2_wireless_room_temperature",    RegisterGroup.Circuit2, false, 0.1, "°C")
            with { Description = "Room temperature received from the wireless sensor for circuit 2 (0.1 °C resolution)" },
        Enum(1076, "status_dhwt_run_stop",                          RegisterGroup.Dhw,      false, (0,"Stop"), (1,"Run"))
            with { Description = "Actual run/stop state of DHW tank heating" },
        Analog(1077, "status_dhwt_setpoint",                        RegisterGroup.Dhw,      false, unit: "°C")
            with { Description = "Active DHW tank setpoint currently used by the unit" },
        Enum(1078, "status_dhw_boost",                              RegisterGroup.Dhw,      false, (0,"Disable"), (1,"Enable"))
            with { Description = "Indicates whether a DHW boost cycle is currently active" },
        Enum(1079, "status_dhw_demand_mode",                        RegisterGroup.Dhw,      false, (0,"Standard"), (1,"High demand"))
            with { Description = "Active DHW demand mode (standard or high priority)" },
        Analog(1080, "status_dhw_temperature",                      RegisterGroup.Dhw,      false, 1.0, "°C", signed: true)
            with { Description = "Measured DHW tank water temperature (signed, 1 °C resolution)" },
        Enum(1081, "status_swimming_pool_run_stop",                  RegisterGroup.Pool,     false, (0,"Stop"), (1,"Run"))
            with { Description = "Actual run/stop state of swimming pool heating" },
        Analog(1082, "status_swimming_pool_setpoint",               RegisterGroup.Pool,     false, unit: "°C")
            with { Description = "Active swimming pool water temperature setpoint" },
        Analog(1083, "status_swimming_pool_temperature",            RegisterGroup.Pool,     false, 1.0, "°C", signed: true)
            with { Description = "Measured swimming pool water temperature (signed, 1 °C resolution)" },
        Enum(1084, "status_anti_legionella_run",                    RegisterGroup.Pool,     false, (0,"Stop"), (1,"Run"))
            with { Description = "Indicates whether an anti-Legionella cycle is currently running" },
        Analog(1085, "status_anti_legionella_setpoint",             RegisterGroup.Pool,     false, unit: "°C")
            with { Description = "Active anti-Legionella disinfection temperature setpoint" },
        Enum(1086, "status_block_menu",                             RegisterGroup.Unit,     false, (0,"No"), (1,"Block"))
            with { Description = "Indicates whether the local HMI menu is currently locked" },
        Enum(1087, "status_bms_alarm",                              RegisterGroup.Unit,     false, (0,"No"), (1,"Alarm"))
            with { Description = "Reflects the BMS alarm input state currently seen by the unit" },
        Enum(1088, "central_mode",                                  RegisterGroup.Unit,     false, (0,"Local"), (1,"Air"), (2,"Water"), (3,"Full"))
            with { Description = "BMS central control mode: Local (unit decides), Air (BMS controls only mode), Water (BMS controls setpoints), Full (BMS controls everything)" },
        Bitmask(1089, "system_configuration",                       RegisterGroup.Unit,     "sys_conf_",
            (0,"circuit1_heating"), (1,"circuit2_heating"),
            (2,"circuit1_cooling"), (3,"circuit2_cooling"),
            (4,"dhwt"), (5,"swimming_pool"),
            (6,"room_thermostat_circuit1"), (7,"room_thermostat_circuit2"),
            (8,"wireless_setpoint_circuit1"), (9,"wireless_setpoint_circuit2"),
            (10,"wireless_room_temp_circuit1"), (11,"wireless_room_temp_circuit2"))
            with { Description = "Bitmask of enabled system features: each bit indicates whether that circuit/function is physically configured and enabled" },
        Enum(1090, "operation_state",                               RegisterGroup.Unit,     false,
            (0,"OFF"), (1,"Cool Demand-OFF"), (2,"Cool Thermo-OFF"), (3,"Cool Thermo-ON"),
            (4,"Heat Demand-OFF"), (5,"Heat Thermo-OFF"), (6,"Heat Thermo-ON"),
            (7,"DHW-OFF"), (8,"DHW-ON"), (9,"SWP-OFF"), (10,"SWP-ON"), (11,"Alarm"))
            with { Description = "Current operation state: OFF=idle, Demand-OFF=demand present but compressor off, Thermo-OFF=thermostat satisfied, Thermo-ON=compressor running, DHW/SWP=hot water or pool mode, Alarm=fault" },
        Analog(1091, "outdoor_ambient_temperature",                 RegisterGroup.Unit,     false, 1.0, "°C", signed: true)
            with { Description = "Outdoor air temperature measured by the unit's outdoor sensor (signed, 1 °C resolution)" },
        Analog(1092, "water_inlet_temperature",                     RegisterGroup.Unit,     false, 1.0, "°C", signed: true)
            with { Description = "Water temperature entering the heat pump (return from heating system); signed, 1 °C resolution" },
        Analog(1093, "water_outlet_temperature",                    RegisterGroup.Unit,     false, 1.0, "°C", signed: true)
            with { Description = "Water temperature leaving the heat pump (supply to heating system); signed, 1 °C resolution" },
        Enum(1094, "hlink_communication_state",                     RegisterGroup.Unit,     false,
            (0,"No alarm"), (1,"No communication with RCS/YUTAKI unit"), (2,"Data initialization"))
            with { Description = "H-LINK bus communication state between the BMS gateway and the main unit controller" },
        Raw(1095, "software_pcb",                                   RegisterGroup.Unit)
            with { Description = "Software version of the main PCB (raw numeric code, decode with Hitachi service documentation)" },
        Raw(1096, "software_lcd",                                   RegisterGroup.Unit)
            with { Description = "Software version of the LCD/HMI controller (raw numeric code)" },
        Analog(1097, "unit_capacity",                               RegisterGroup.Unit,     false, 1.0, "kWh")
            with { Description = "Cumulative thermal energy delivered by the heat pump (monotonically increasing counter, wraps at 65535 kWh)" },
        Analog(1098, "unit_power_consumption",                      RegisterGroup.Unit,     false, 1.0, "kWh")
            with { Description = "Cumulative electrical energy consumed by the heat pump (monotonically increasing counter, wraps at 65535 kWh)" },

        // ── Servicing parameters (addresses 1200–1231) ─────────────────────────
        Analog(1200, "svc_water_outlet_hp_temperature",         RegisterGroup.Diagnostics, false, 1.0,  "°C")
            with { Description = "Water outlet temperature measured directly at the heat pump heat exchanger (may differ from unit_outlet due to sensor placement)" },
        Analog(1201, "svc_outdoor_ambient_average_temperature", RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Running average of outdoor ambient temperature used internally by the OTC curve calculations (signed, 1 °C)" },
        Analog(1202, "svc_second_ambient_temperature",          RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Instantaneous reading of a second outdoor ambient sensor (signed, 1 °C)" },
        Analog(1203, "svc_second_ambient_average_temperature",  RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Running average of the second ambient sensor (signed, 1 °C)" },
        Analog(1204, "svc_water_outlet_temperature_2",          RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Water outlet temperature at a secondary point in the hydraulic circuit (signed, 1 °C)" },
        Analog(1205, "svc_water_outlet_temperature_3",          RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Water outlet temperature at a third measuring point (signed, 1 °C)" },
        Analog(1206, "svc_gas_temperature",                     RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Refrigerant gas temperature in the refrigerant circuit (suction line or mid-circuit sensor, signed, 1 °C)" },
        Analog(1207, "svc_liquid_temperature",                  RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Refrigerant liquid line temperature after the condenser (signed, 1 °C)" },
        Analog(1208, "svc_discharge_gas_temperature",           RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Compressor discharge gas temperature; high values indicate refrigerant shortage or compressor overheating (signed, 1 °C)" },
        Analog(1209, "svc_evaporation_temperature",             RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Refrigerant evaporation temperature (saturation temperature at low-pressure side); approaches outdoor temperature in normal operation (signed, 1 °C)" },
        Analog(1210, "svc_indoor_expansion_valve",              RegisterGroup.Diagnostics, false, 1.0,  "%")
            with { Description = "Electronic expansion valve opening on the indoor/hydraulic side (0–100 %)" },
        Analog(1211, "svc_outdoor_expansion_valve",             RegisterGroup.Diagnostics, false, 1.0,  "%")
            with { Description = "Electronic expansion valve opening on the outdoor/refrigerant side (0–100 %)" },
        Analog(1212, "svc_compressor_frequency",                RegisterGroup.Diagnostics, false, 1.0,  "Hz")
            with { Description = "Inverter compressor operating frequency; higher frequency = more capacity (typical range 20–120 Hz)" },
        Raw  (1213, "svc_cause_of_stoppage",                    RegisterGroup.Diagnostics)
            with { Description = "Reason code for the last compressor or unit stoppage (raw code, decode with Hitachi service manual)" },
        Analog(1214, "svc_compressor_current",                  RegisterGroup.Diagnostics, false, 0.1,  "A")
            with { Description = "Compressor motor current draw (0.1 A resolution); useful for monitoring load and detecting anomalies" },
        Raw  (1215, "svc_capacity_data",                        RegisterGroup.Diagnostics)
            with { Description = "Internal capacity step or stage indicator used by the unit controller (raw, meaning depends on model)" },
        Analog(1216, "svc_mixing_valve_position",               RegisterGroup.Diagnostics, false, 1.0,  "%")
            with { Description = "Position of the hydraulic mixing valve (0 % = fully toward cold, 100 % = fully toward hot)" },
        Raw  (1217, "svc_defrosting",                           RegisterGroup.Diagnostics)
            with { Description = "Defrost status flags (raw bitmask; bit meaning varies by model — check service documentation)" },
        Enum (1218, "svc_unit_model",                           RegisterGroup.Diagnostics, false, (0,"YUTAKI S"), (1,"YUTAKI S COMBI"), (2,"S80"), (3,"M"))
            with { Description = "Heat pump product variant as identified by the unit's own firmware" },
        Analog(1219, "svc_water_temp_setting",                  RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "Internal water temperature target currently being pursued by the unit controller (may differ from user setpoint due to protection logic)" },
        Analog(1220, "svc_water_flow",                          RegisterGroup.Diagnostics, false, 0.1,  "m³/h")
            with { Description = "Water flow rate through the heat pump (0.1 m³/h resolution); low flow triggers protection" },
        Analog(1221, "svc_water_pump_speed",                    RegisterGroup.Diagnostics, false, 1.0,  "%")
            with { Description = "Variable-speed water pump duty cycle (0–100 %)" },
        Bitmask(1222, "svc_system_status",                      RegisterGroup.Diagnostics, "svc_sys_status_",
            (0,"defrost"), (1,"solar"), (2,"water_pump_1"), (3,"water_pump_2"), (4,"water_pump_3"),
            (5,"compressor_on"), (6,"boiler_on"), (7,"dhw_heater"), (8,"space_heater"), (9,"smart_function_input"))
            with { Description = "Real-time system status bitmask: each bit indicates whether that component or function is currently active" },
        Raw  (1223, "svc_alarm_number",                         RegisterGroup.Diagnostics)
            with { Description = "Current or last active alarm code (raw number, decode with Hitachi alarm code table)" },
        Analog(1224, "svc_r134a_discharge_temperature",         RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "R134a (high-temperature booster) compressor discharge temperature (YUTAKI S COMBI / S80 only; signed, 1 °C)" },
        Analog(1225, "svc_r134a_suction_temperature",           RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true)
            with { Description = "R134a compressor suction temperature (YUTAKI S COMBI / S80 only; signed, 1 °C)" },
        Analog(1226, "svc_r134a_discharge_pressure",            RegisterGroup.Diagnostics, false, 0.01, "MPa")
            with { Description = "R134a high-pressure side pressure (0.01 MPa resolution; YUTAKI S COMBI / S80 only)" },
        Analog(1227, "svc_r134a_suction_pressure",              RegisterGroup.Diagnostics, false, 0.01, "MPa")
            with { Description = "R134a low-pressure side (suction) pressure (0.01 MPa resolution; YUTAKI S COMBI / S80 only)" },
        Analog(1228, "svc_r134a_compressor_frequency",          RegisterGroup.Diagnostics, false, 1.0,  "Hz")
            with { Description = "R134a booster compressor inverter frequency (YUTAKI S COMBI / S80 only)" },
        Analog(1229, "svc_r134a_expansion_valve",               RegisterGroup.Diagnostics, false, 1.0,  "%")
            with { Description = "R134a circuit electronic expansion valve opening (YUTAKI S COMBI / S80 only, 0–100 %)" },
        Analog(1230, "svc_r134a_compressor_current",            RegisterGroup.Diagnostics, false, 0.1,  "A")
            with { Description = "R134a booster compressor current draw (0.1 A resolution; YUTAKI S COMBI / S80 only)" },
        Raw  (1231, "svc_r134a_retry_code",                     RegisterGroup.Diagnostics)
            with { Description = "R134a subsystem retry/error code (raw; YUTAKI S COMBI / S80 only)" },
    ];

    public static readonly IReadOnlyDictionary<string, RegisterDefinition> ByName =
        All.ToDictionary(r => r.Name);
}
