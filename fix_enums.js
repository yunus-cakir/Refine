const fs = require('fs');
let content = fs.readFileSync('c:/Library/Projects/Visual Studio/Refine/Refine.App/Services/LocalDbService.cs', 'utf8');

content = content.replace(/Equipment = Exercise\.EquipmentType\.,/g, 'Equipment = "Machine",'); // temporary revert for broken ones, wait we don't know what they were.
