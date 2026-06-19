Place CMHC_Upload_Template.xlsx here for local dev fallback, or upload it to the Fabric lakehouse folder:
  Files/excel_files/cmhc/CMHC_Upload_Template.xlsx

Production uploads use Microsoft Fabric OneLake (CmhcUpload:Fabric* settings in appsettings.json).
The file name must match CmhcUpload:TemplateFileName in appsettings.json.
