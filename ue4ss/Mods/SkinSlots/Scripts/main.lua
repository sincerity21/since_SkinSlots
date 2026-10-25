-- SkinSlots
-- Registers converted skin paks as new hangar slots.
-- Each converted pak stays a pawn plus materials. This mod inserts the SkinDataTable row
-- and answers the skin name keys. Drop *.skin_slot.ini files into the slots folder beside this script.
--
-- UE4SS exposes UDataTable:AddRow, which calls the native AddRow. The DLL next to this script
-- records the manifests it can see. The row write stays here so it uses that binding.

local TAG = "[SkinSlots] "
local function log(text) print(TAG .. tostring(text) .. "\n") end

local scriptDir = ""
do
    local ok, info = pcall(function() return debug.getinfo(1, "S") end)
    if ok and info and type(info.source) == "string" then
        scriptDir = info.source:match("^@(.*[\\/])") or ""
    end
end

local function valid(object)
    return object and object.IsValid and object:IsValid()
end

local function trim(value)
    return (tostring(value or ""):gsub("^%s+", ""):gsub("%s+$", ""))
end

local function readIni(path)
    local slot = {}
    local handle = io.open(path, "r")
    if not handle then return nil end
    for line in handle:lines() do
        local key, value = line:match("^%s*([%w_]+)%s*=%s*(.*)$")
        if key then slot[key] = trim(value) end
    end
    handle:close()
    if slot.plane == nil or slot.skin_no == nil or slot.pawn == nil or slot.row_name == nil then
        return nil
    end
    return slot
end

local function loadSlots()
    local slots = {}
    local folder = scriptDir .. "slots"
    local command = 'dir /b "' .. folder .. '\\*.skin_slot.ini" 2>nul'
    local pipe = io.popen(command)
    if not pipe then return slots end
    for name in pipe:lines() do
        local slot = readIni(folder .. "\\" .. name)
        if slot then slots[#slots + 1] = slot
        else log("skipped unreadable manifest " .. name) end
    end
    pipe:close()
    return slots
end

local function textKeys(slot)
    local number = tonumber(slot.skin_no) or 0
    local keys = {}
    local description = slot.description
    if description == nil or description == "" then description = slot.name or slot.plane end
    keys["AircraftSkin_Description_" .. slot.plane .. "_" .. string.format("%02d", number)] = description
    keys["AircraftSkintype_Name_" .. slot.plane .. "_DLC" .. tostring(number - 5)] = slot.name or slot.plane
    return keys
end

local texts = {}
local function rebuildTexts(slots)
    texts = {}
    for _, slot in ipairs(slots) do
        for key, value in pairs(textKeys(slot)) do
            if value ~= nil and value ~= "" then texts[key] = value end
        end
    end
end

local function paramText(param)
    if param == nil then return "" end
    local ok, value = pcall(function()
        if type(param) == "string" then return param end
        if param.get then return param:get() end
        return param
    end)
    if not ok or value == nil then return "" end
    if type(value) == "string" then return value end
    local okText, text = pcall(function() return value:ToString() end)
    if okText and type(text) == "string" then return text end
    return tostring(value)
end

local function hookKey(key)
    local text = paramText(key)
    local custom = texts[text]
    if custom == nil then return nil end
    return custom
end

local hooksReady = false
local function installHooks()
    if hooksReady or type(RegisterHook) ~= "function" then return end
    local has = StaticFindObject("/Script/Nimbus.LocalizationManager:HasString")
    local localize = StaticFindObject("/Script/Nimbus.LocalizationManager:LocalizeString")
    if not valid(has) or not valid(localize) then return end

    RegisterHook("/Script/Nimbus.LocalizationManager:HasString", function() end, function(_, key)
        if hookKey(key) == nil then return nil end
        return true
    end)
    RegisterHook("/Script/Nimbus.LocalizationManager:LocalizeString", function() end, function(_, key)
        local custom = hookKey(key)
        if custom == nil or type(FText) ~= "function" then return nil end
        return FText(custom)
    end)
    hooksReady = true
    log("name hooks installed")
end

local function fieldText(row, field)
    local ok, value = pcall(function() return row[field] end)
    if not ok then return "" end
    return paramText(value)
end

local function assign(row, field, value)
    local ok = pcall(function() row[field] = value end)
    if ok then return true end
    ok = pcall(function() row:SetPropertyValue(field, value) end)
    return ok
end

local function findDonor(skinTable, plane)
    local donor = nil
    local walked = pcall(function()
        skinTable:ForEachRow(function(_, row)
            if donor then return true end
            if fieldText(row, "PlaneStringID"):lower() == plane then
                donor = row
                return true
            end
        end)
    end)
    if not walked then return nil end
    return donor
end

local function remember(inserted, slot)
    inserted[slot.row_name] = true
end

local function alreadyThere(skinTable, rowName, inserted)
    if inserted[rowName] then return true end
    local found = false
    pcall(function()
        skinTable:ForEachRow(function(name)
            if paramText(name) == rowName then found = true return true end
        end)
    end)
    if found then inserted[rowName] = true end
    return found
end

local function insertSlot(skinTable, slot, inserted)
    if alreadyThere(skinTable, slot.row_name, inserted) then return end
    local plane = slot.plane:lower()
    local skinNo = tonumber(slot.skin_no) or 0
    local skinId = tonumber(slot.skin_id) or skinNo
    local sortNumber = tonumber(slot.sort_number) or (1000 + skinNo)
    local donor = findDonor(skinTable, plane)

    local function fill(row)
        assign(row, "PlaneStringID", plane)
        assign(row, "SkinNo", skinNo)
        assign(row, "SkinID", skinId)
        assign(row, "SortNumber", sortNumber)
        assign(row, "PlaneReference", slot.pawn)
        if slot.description and slot.description ~= "" then
            assign(row, "SkinName", slot.name or "")
        end
    end

    if donor then
        local copied = pcall(function() skinTable:AddRow(slot.row_name, donor) end)
        if copied then
            local created = nil
            pcall(function() created = skinTable:FindRow(slot.row_name) end)
            local applied = false
            if created then
                fill(created)
                applied = tonumber(fieldText(created, "SkinNo")) == skinNo
            end
            if applied then
                remember(inserted, slot)
                log("inserted " .. slot.row_name .. " from " .. plane)
                return
            end
            -- The copied row still has the donor's skin number. Drop it and write a fresh row.
            pcall(function() skinTable:RemoveRow(slot.row_name) end)
        end
    end

    local tableRow = {
        PlaneStringID = plane,
        SkinNo = skinNo,
        SkinID = skinId,
        SortNumber = sortNumber,
        PlaneReference = slot.pawn,
        bNoseEmblem = true,
        bWingEmblem = true,
        bTailEmblem = true,
        DLCID = ""
    }
    local ok, err = pcall(function() skinTable:AddRow(slot.row_name, tableRow) end)
    if ok then
        remember(inserted, slot)
        log("inserted " .. slot.row_name)
    else
        log("AddRow failed for " .. slot.row_name .. ": " .. tostring(err))
    end
end

local function skinTableOf(instance)
    if not valid(instance) then return nil end
    local info = instance.GameInformation
    if not valid(info) then return nil end
    local table = info.SkinDataTable
    if not valid(table) then return nil end
    return table
end

local inserted = {}
local function tick()
    local slots = loadSlots()
    rebuildTexts(slots)
    installHooks()
    if #slots == 0 then return end

    local instances = {}
    pcall(function() instances = FindAllOf("NimbusGameInstance") or {} end)
    for _, instance in ipairs(instances) do
        local skinTable = skinTableOf(instance)
        if skinTable then
            for _, slot in ipairs(slots) do
                insertSlot(skinTable, slot, inserted)
            end
        end
    end
end

local function onGameThread(fn)
    if type(ExecuteInGameThread) == "function" then ExecuteInGameThread(fn) else fn() end
end

local tickNative = nil
if type(package) == "table" and type(package.loadlib) == "function" then
    local loaded, err = package.loadlib(scriptDir .. "SkinSlots.dll", "luaopen_skinslots")
    tickNative = package.loadlib(scriptDir .. "SkinSlots.dll", "skinslots_tick")
    if loaded then
        onGameThread(function() pcall(loaded) end)
        log("SkinSlots.dll loaded")
    else
        log("SkinSlots.dll was not loaded (" .. tostring(err) .. "). Row insert still runs from this script.")
    end
end

if type(LoopAsync) ~= "function" then
    log("this UE4SS build has no LoopAsync")
    return
end

LoopAsync(1000, function()
    onGameThread(function()
        if tickNative then pcall(tickNative) end
        local ok, err = pcall(tick)
        if not ok then log("tick failed: " .. tostring(err)) end
    end)
    return false
end)

log("loaded")
