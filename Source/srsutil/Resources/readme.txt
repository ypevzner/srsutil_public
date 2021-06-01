1. Install Visual Studio 2019, .NET 5.0, Python 3.7.9, Git 2.29.2, and Cmake 3.17.1.

https://visualstudio.microsoft.com/vs/community/
https://dotnet.microsoft.com/download
https://www.python.org/downloads/windows/
https://git-scm.com/download/
https://cmake.org/download/

2. Clone Indigo source code

git clone https://github.com/epam/Indigo.git

3. Download InChI source code from https://www.inchi-trust.org/downloads/

curl https://www.inchi-trust.org/download/106/INCHI-1-SRC.zip -o INCHI-1-SRC.zip

4. Unzip and copy InChI source files into Indigo tree:

unzip INCHI-1-SRC.zip
cd Indigo/third_party/inchi
rm -fR INCHI_API/ INCHI_BASE/
cp -auf ../../../INCHI-1-SRC/INCHI_API/ .
cp -auf ../../../INCHI-1-SRC/INCHI_BASE/ .

5. Edit Indigo/build_scripts/indigo-make-by-libs.py to keep the version in the x.y.z format (e.g. 1.4.1):

--- indigo-make-by-libs.py.orig 2020-12-10 10:15:47.963891800 -0500
+++ indigo-make-by-libs.py      2020-12-10 10:58:05.830331300 -0500
@@ -154,6 +154,10 @@
     sys.path.append(os.path.join(os.path.dirname(__file__), os.pardir, "api"))
     get_indigo_version = __import__('get_indigo_version').getIndigoVersion
     version = get_indigo_version()
+    m = re.search('(\d+\.\d+\.\d+).*', version)
+    if m:
+        version = m.group(1)
+
     with cwd(os.path.join(os.path.split(__file__)[0], '..', 'dist')):

         if need_join_archives:

6. Run the build scripts (in Indigo folder) - the output will appear in Indigo/dist:

python build_scripts/indigo-release-libs.py --preset=win64-2019
python build_scripts/indigo-release-utils.py --preset=win64-2019
python build_scripts/indigo-make-by-libs.py --type=dotnet

7. Unzip srsutil source and open srsutil-yulia/Source/SRS.sln in Visual Studio 2019:
unzip srsutil-yulia.zip

8. Remove InChI, Indigo, and OpenBabel from *.csproj and package.config
9. Edit InChi.cs to get rid of dependency on OpenBabel
--- InChI.cs.orig       2021-01-08 13:10:25.407122400 -0500
+++ InChI.cs    2021-01-08 14:05:27.803583200 -0500
@@ -1,6 +1,4 @@
-﻿using com.ggasoftware.indigo;
-using OpenBabelNet;
-
+﻿using com.epam.indigo;

 namespace FDA.SRS.Utils
 {
@@ -22,13 +20,19 @@

         public static string InChIToMol(string inchi, bool clean = true)
         {
-            string str = OpenBabel.GetInstance().convert(inchi, "inchi", "mol");
+            var oindigo = new Indigo();
+           var indigo = new IndigoInchi(oindigo);
+            var molecule = indigo.loadMolecule(inchi);
+           string str = molecule.molfile();
             return (clean ? Clean(str) : str);
         }

         public static string InChIToSMILES(string inchi)
         {
-            string str = OpenBabel.GetInstance().convert(inchi, "inchi", "smiles").Trim();
+            var oindigo = new Indigo();
+           var indigo = new IndigoInchi(oindigo);
+            var molecule = indigo.loadMolecule(inchi);
+           string str = molecule.smiles().Trim();
             return str;
         }
     }


10. Edit .cs files to replace

using com.ggasoftware.indigo;
with
using com.epam.indigo;

11. Right click on projects FDA.SRS.Utils, FDA.SRS.ObjectModel, FDA.SRS.Services select "Add", select "Add reference", then "Browse" and browse to Indigo.Net.dll
Indigo\api\dotnet\bin\Release\netstandard2.0

12. Edit lines in ChemToolkitTests.cs in function getSGroupInfo: replace getSgroup... with getSGroup...

13. Update NuGet packages for SRSTests project

14. Right click on srsutil project, select "properties". Check "Auto-generate binding redirects" checkbox.

15. BUILD THE SOLUTION !!!

16.  cp Indigo/api/dotnet/bin/Release/netstandard2.0/Indigo.Net.dll srsutil-yulia/Source/srsutil/bin/Release/
     cp Indigo/api/dotnet/bin/Release/netstandard2.0/lib/netstandard2.0/*.dll srsutil-yulia/Source/srsutil/bin/Release/

17. Run the tool - cmd or powershell, not git bash:
cd Source\srsutil\bin\Release
srsutil.exe /command=str2spl /if=..\\..\\..\\..\\..\srs\\Examples\public_chemicals_V3000.sdf /odir=..\\..\\..\\..\\..\\out


