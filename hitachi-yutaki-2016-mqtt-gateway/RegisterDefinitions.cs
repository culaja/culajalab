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
        Enum(1000, "unit_run_stop",                         RegisterGroup.Unit,     true,  (0,"Stop"), (1,"Run")),
        Enum(1001, "unit_mode",                             RegisterGroup.Unit,     true,  (0,"Cool"), (1,"Heat"), (2,"Auto")),
        Enum(1002, "circuit1_run_stop",                     RegisterGroup.Circuit1, true,  (0,"Stop"), (1,"Run")),
        Enum(1003, "circuit1_heat_otc",                     RegisterGroup.Circuit1, true,  (0,"No"), (1,"Points"), (2,"Gradient"), (3,"Fix")),
        Enum(1004, "circuit1_cool_otc",                     RegisterGroup.Circuit1, true,  (0,"No"), (1,"Points"), (2,"Fix")),
        Analog(1005, "circuit1_water_heating_setpoint",     RegisterGroup.Circuit1, true,  unit: "°C"),
        Analog(1006, "circuit1_water_cooling_setpoint",     RegisterGroup.Circuit1, true,  unit: "°C"),
        Enum(1007, "circuit1_eco_mode",                     RegisterGroup.Circuit1, true,  (0,"ECO"), (1,"Comfort")),
        Analog(1008, "circuit1_heat_eco_offset",            RegisterGroup.Circuit1, true),
        Analog(1009, "circuit1_cool_eco_offset",            RegisterGroup.Circuit1, true),
        Enum(1010, "circuit1_thermostat_available",         RegisterGroup.Circuit1, true,  (0,"Not Available"), (1,"Available")),
        Analog(1011, "circuit1_thermostat_setpoint",        RegisterGroup.Circuit1, true,  0.1, "°C"),
        Analog(1012, "circuit1_thermostat_room_temperature",RegisterGroup.Circuit1, true,  0.1, "°C"),
        Enum(1013, "circuit2_run_stop",                     RegisterGroup.Circuit2, true,  (0,"Stop"), (1,"Run")),
        Enum(1014, "circuit2_heat_otc",                     RegisterGroup.Circuit2, true,  (0,"No"), (1,"Points"), (2,"Gradient"), (3,"Fix")),
        Enum(1015, "circuit2_cool_otc",                     RegisterGroup.Circuit2, true,  (0,"No"), (1,"Points"), (2,"Fix")),
        Analog(1016, "circuit2_water_heating_setpoint",     RegisterGroup.Circuit2, true,  unit: "°C"),
        Analog(1017, "circuit2_water_cooling_setpoint",     RegisterGroup.Circuit2, true,  unit: "°C"),
        Enum(1018, "circuit2_eco_mode",                     RegisterGroup.Circuit2, true,  (0,"ECO"), (1,"Comfort")),
        Analog(1019, "circuit2_heat_eco_offset",            RegisterGroup.Circuit2, true),
        Analog(1020, "circuit2_cool_eco_offset",            RegisterGroup.Circuit2, true),
        Enum(1021, "circuit2_thermostat_available",         RegisterGroup.Circuit2, true,  (0,"Not Available"), (1,"Available")),
        Analog(1022, "circuit2_thermostat_setpoint",        RegisterGroup.Circuit2, true,  0.1, "°C"),
        Analog(1023, "circuit2_thermostat_room_temperature",RegisterGroup.Circuit2, true,  0.1, "°C"),
        Enum(1024, "dhwt_run_stop",                         RegisterGroup.Dhw,      true,  (0,"Stop"), (1,"Run")),
        Analog(1025, "dhwt_setpoint",                       RegisterGroup.Dhw,      true,  unit: "°C"),
        Enum(1026, "dhw_boost",                             RegisterGroup.Dhw,      true,  (0,"No request"), (1,"Request")),
        Enum(1027, "dhw_demand_mode",                       RegisterGroup.Dhw,      true,  (0,"Standard"), (1,"High demand")),
        Enum(1028, "swimming_pool_run_stop",                RegisterGroup.Pool,     true,  (0,"Stop"), (1,"Run")),
        Analog(1029, "swimming_pool_setpoint",              RegisterGroup.Pool,     true,  unit: "°C"),
        Enum(1030, "anti_legionella_run",                   RegisterGroup.Pool,     true,  (0,"Stop"), (1,"Run")),
        Analog(1031, "anti_legionella_setpoint",            RegisterGroup.Pool,     true,  unit: "°C"),
        Enum(1032, "block_menu",                            RegisterGroup.Unit,     true,  (0,"No"), (1,"Block")),
        Enum(1033, "bms_alarm",                             RegisterGroup.Unit,     true,  (0,"No Alarm"), (1,"Alarm")),

        // ── Status R (addresses 1050–1098) ─────────────────────────────────────
        Enum(1050, "status_unit_run_stop",                          RegisterGroup.Unit,     false, (0,"Stop"), (1,"Run")),
        Enum(1051, "status_unit_mode",                              RegisterGroup.Unit,     false, (0,"Cool"), (1,"Heat")),
        Enum(1052, "status_circuit1_run_stop",                      RegisterGroup.Circuit1, false, (0,"Stop"), (1,"Run")),
        Enum(1053, "status_circuit1_heat_otc",                      RegisterGroup.Circuit1, false, (0,"No"), (1,"Points"), (2,"Gradient"), (3,"Fix")),
        Enum(1054, "status_circuit1_cool_otc",                      RegisterGroup.Circuit1, false, (0,"No"), (1,"Points"), (2,"Fix")),
        Analog(1055, "status_circuit1_water_heating_setpoint",      RegisterGroup.Circuit1, false, unit: "°C"),
        Analog(1056, "status_circuit1_water_cooling_setpoint",      RegisterGroup.Circuit1, false, unit: "°C"),
        Enum(1057, "status_circuit1_eco_mode",                      RegisterGroup.Circuit1, false, (0,"ECO"), (1,"Comfort")),
        Analog(1058, "status_circuit1_heat_eco_offset",             RegisterGroup.Circuit1, false),
        Analog(1059, "status_circuit1_cool_eco_offset",             RegisterGroup.Circuit1, false),
        Analog(1060, "status_circuit1_thermostat_setpoint",         RegisterGroup.Circuit1, false, 0.1, "°C"),
        Analog(1061, "status_circuit1_thermostat_room_temperature",  RegisterGroup.Circuit1, false, 0.1, "°C"),
        Analog(1062, "status_circuit1_wireless_setpoint",           RegisterGroup.Circuit1, false, 0.1, "°C"),
        Analog(1063, "status_circuit1_wireless_room_temperature",    RegisterGroup.Circuit1, false, 0.1, "°C"),
        Enum(1064, "status_circuit2_run_stop",                      RegisterGroup.Circuit2, false, (0,"Stop"), (1,"Run")),
        Enum(1065, "status_circuit2_heat_otc",                      RegisterGroup.Circuit2, false, (0,"No"), (1,"Points"), (2,"Gradient"), (3,"Fix")),
        Enum(1066, "status_circuit2_cool_otc",                      RegisterGroup.Circuit2, false, (0,"No"), (1,"Points"), (2,"Fix")),
        Analog(1067, "status_circuit2_water_heating_setpoint",      RegisterGroup.Circuit2, false, unit: "°C"),
        Analog(1068, "status_circuit2_water_cooling_setpoint",      RegisterGroup.Circuit2, false, unit: "°C"),
        Enum(1069, "status_circuit2_eco_mode",                      RegisterGroup.Circuit2, false, (0,"ECO"), (1,"Comfort")),
        Analog(1070, "status_circuit2_heat_eco_offset",             RegisterGroup.Circuit2, false),
        Analog(1071, "status_circuit2_cool_eco_offset",             RegisterGroup.Circuit2, false),
        Analog(1072, "status_circuit2_thermostat_setpoint",         RegisterGroup.Circuit2, false, 0.1, "°C"),
        Analog(1073, "status_circuit2_thermostat_room_temperature",  RegisterGroup.Circuit2, false, 0.1, "°C"),
        Analog(1074, "status_circuit2_wireless_setpoint",           RegisterGroup.Circuit2, false, 0.1, "°C"),
        Analog(1075, "status_circuit2_wireless_room_temperature",    RegisterGroup.Circuit2, false, 0.1, "°C"),
        Enum(1076, "status_dhwt_run_stop",                          RegisterGroup.Dhw,      false, (0,"Stop"), (1,"Run")),
        Analog(1077, "status_dhwt_setpoint",                        RegisterGroup.Dhw,      false, unit: "°C"),
        Enum(1078, "status_dhw_boost",                              RegisterGroup.Dhw,      false, (0,"Disable"), (1,"Enable")),
        Enum(1079, "status_dhw_demand_mode",                        RegisterGroup.Dhw,      false, (0,"Standard"), (1,"High demand")),
        Analog(1080, "status_dhw_temperature",                      RegisterGroup.Dhw,      false, 1.0, "°C", signed: true),
        Enum(1081, "status_swimming_pool_run_stop",                  RegisterGroup.Pool,     false, (0,"Stop"), (1,"Run")),
        Analog(1082, "status_swimming_pool_setpoint",               RegisterGroup.Pool,     false, unit: "°C"),
        Analog(1083, "status_swimming_pool_temperature",            RegisterGroup.Pool,     false, 1.0, "°C", signed: true),
        Enum(1084, "status_anti_legionella_run",                    RegisterGroup.Pool,     false, (0,"Stop"), (1,"Run")),
        Analog(1085, "status_anti_legionella_setpoint",             RegisterGroup.Pool,     false, unit: "°C"),
        Enum(1086, "status_block_menu",                             RegisterGroup.Unit,     false, (0,"No"), (1,"Block")),
        Enum(1087, "status_bms_alarm",                              RegisterGroup.Unit,     false, (0,"No"), (1,"Alarm")),
        Enum(1088, "central_mode",                                  RegisterGroup.Unit,     false, (0,"Local"), (1,"Air"), (2,"Water"), (3,"Full")),
        Bitmask(1089, "system_configuration",                       RegisterGroup.Unit,     "sys_conf_",
            (0,"circuit1_heating"), (1,"circuit2_heating"),
            (2,"circuit1_cooling"), (3,"circuit2_cooling"),
            (4,"dhwt"), (5,"swimming_pool"),
            (6,"room_thermostat_circuit1"), (7,"room_thermostat_circuit2"),
            (8,"wireless_setpoint_circuit1"), (9,"wireless_setpoint_circuit2"),
            (10,"wireless_room_temp_circuit1"), (11,"wireless_room_temp_circuit2")),
        Enum(1090, "operation_state",                               RegisterGroup.Unit,     false,
            (0,"OFF"), (1,"Cool Demand-OFF"), (2,"Cool Thermo-OFF"), (3,"Cool Thermo-ON"),
            (4,"Heat Demand-OFF"), (5,"Heat Thermo-OFF"), (6,"Heat Thermo-ON"),
            (7,"DHW-OFF"), (8,"DHW-ON"), (9,"SWP-OFF"), (10,"SWP-ON"), (11,"Alarm")),
        Analog(1091, "outdoor_ambient_temperature",                 RegisterGroup.Unit,     false, 1.0, "°C", signed: true),
        Analog(1092, "water_inlet_temperature",                     RegisterGroup.Unit,     false, 1.0, "°C", signed: true),
        Analog(1093, "water_outlet_temperature",                    RegisterGroup.Unit,     false, 1.0, "°C", signed: true),
        Enum(1094, "hlink_communication_state",                     RegisterGroup.Unit,     false,
            (0,"No alarm"), (1,"No communication with RCS/YUTAKI unit"), (2,"Data initialization")),
        Raw(1095, "software_pcb",                                   RegisterGroup.Unit),
        Raw(1096, "software_lcd",                                   RegisterGroup.Unit),
        Analog(1097, "unit_capacity",                               RegisterGroup.Unit,     false, 1.0, "kWh"),
        Analog(1098, "unit_power_consumption",                      RegisterGroup.Unit,     false, 1.0, "kWh"),

        // ── Servicing parameters (addresses 1200–1231) ─────────────────────────
        Analog(1200, "svc_water_outlet_hp_temperature",         RegisterGroup.Diagnostics, false, 1.0,  "°C"),
        Analog(1201, "svc_outdoor_ambient_average_temperature", RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1202, "svc_second_ambient_temperature",          RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1203, "svc_second_ambient_average_temperature",  RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1204, "svc_water_outlet_temperature_2",          RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1205, "svc_water_outlet_temperature_3",          RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1206, "svc_gas_temperature",                     RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1207, "svc_liquid_temperature",                  RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1208, "svc_discharge_gas_temperature",           RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1209, "svc_evaporation_temperature",             RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1210, "svc_indoor_expansion_valve",              RegisterGroup.Diagnostics, false, 1.0,  "%"),
        Analog(1211, "svc_outdoor_expansion_valve",             RegisterGroup.Diagnostics, false, 1.0,  "%"),
        Analog(1212, "svc_compressor_frequency",                RegisterGroup.Diagnostics, false, 1.0,  "Hz"),
        Raw  (1213, "svc_cause_of_stoppage",                    RegisterGroup.Diagnostics),
        Analog(1214, "svc_compressor_current",                  RegisterGroup.Diagnostics, false, 0.1,  "A"),
        Raw  (1215, "svc_capacity_data",                        RegisterGroup.Diagnostics),
        Analog(1216, "svc_mixing_valve_position",               RegisterGroup.Diagnostics, false, 1.0,  "%"),
        Raw  (1217, "svc_defrosting",                           RegisterGroup.Diagnostics),
        Enum (1218, "svc_unit_model",                           RegisterGroup.Diagnostics, false, (0,"YUTAKI S"), (1,"YUTAKI S COMBI"), (2,"S80"), (3,"M")),
        Analog(1219, "svc_water_temp_setting",                  RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1220, "svc_water_flow",                          RegisterGroup.Diagnostics, false, 0.1,  "m³/h"),
        Analog(1221, "svc_water_pump_speed",                    RegisterGroup.Diagnostics, false, 1.0,  "%"),
        Bitmask(1222, "svc_system_status",                      RegisterGroup.Diagnostics, "svc_sys_status_",
            (0,"defrost"), (1,"solar"), (2,"water_pump_1"), (3,"water_pump_2"), (4,"water_pump_3"),
            (5,"compressor_on"), (6,"boiler_on"), (7,"dhw_heater"), (8,"space_heater"), (9,"smart_function_input")),
        Raw  (1223, "svc_alarm_number",                         RegisterGroup.Diagnostics),
        Analog(1224, "svc_r134a_discharge_temperature",         RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1225, "svc_r134a_suction_temperature",           RegisterGroup.Diagnostics, false, 1.0,  "°C", signed: true),
        Analog(1226, "svc_r134a_discharge_pressure",            RegisterGroup.Diagnostics, false, 0.01, "MPa"),
        Analog(1227, "svc_r134a_suction_pressure",              RegisterGroup.Diagnostics, false, 0.01, "MPa"),
        Analog(1228, "svc_r134a_compressor_frequency",          RegisterGroup.Diagnostics, false, 1.0,  "Hz"),
        Analog(1229, "svc_r134a_expansion_valve",               RegisterGroup.Diagnostics, false, 1.0,  "%"),
        Analog(1230, "svc_r134a_compressor_current",            RegisterGroup.Diagnostics, false, 0.1,  "A"),
        Raw  (1231, "svc_r134a_retry_code",                     RegisterGroup.Diagnostics),
    ];

    public static readonly IReadOnlyDictionary<string, RegisterDefinition> ByName =
        All.ToDictionary(r => r.Name);
}
