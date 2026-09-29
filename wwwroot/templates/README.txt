Place CMHC_Upload_Template.xlsx here for local dev fallback, or upload it to the Fabric lakehouse folder:
  Files/external_files/cmhc_file/CMHC_Upload_Template.xlsx

CMHC Excel uploads: Files/external_files/cmhc_file/
QR slides PDF uploads: Files/external_files/qr_slides/

Production uploads use Microsoft Fabric OneLake (CmhcUpload settings in appsettings.json).
The file name must match CmhcUpload:TemplateFileName in appsettings.json.
