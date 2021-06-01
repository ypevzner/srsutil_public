#----------------------------------------------------------------
# Generated CMake target import file for configuration "Release".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "Inchi" for configuration "Release"
set_property(TARGET Inchi APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Inchi PROPERTIES
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libInchi.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libInchi.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS Inchi )
list(APPEND _IMPORT_CHECK_FILES_FOR_Inchi "/home/igor/repo/fda/rdkit-master/lib/libInchi.so.1.2015.03.1pre" )

# Import target "Inchi_static" for configuration "Release"
set_property(TARGET Inchi_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Inchi_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "C"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libInchi_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS Inchi_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_Inchi_static "/home/igor/repo/fda/rdkit-master/lib/libInchi_static.a" )

# Import target "RDInchiLib" for configuration "Release"
set_property(TARGET RDInchiLib APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(RDInchiLib PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "Inchi;GraphMol;RDGeneral;Depictor;SubstructMatch;FileParsers;SmilesParse;-lpthread"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libRDInchiLib.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libRDInchiLib.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS RDInchiLib )
list(APPEND _IMPORT_CHECK_FILES_FOR_RDInchiLib "/home/igor/repo/fda/rdkit-master/lib/libRDInchiLib.so.1.2015.03.1pre" )

# Import target "RDInchiLib_static" for configuration "Release"
set_property(TARGET RDInchiLib_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(RDInchiLib_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libRDInchiLib_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS RDInchiLib_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_RDInchiLib_static "/home/igor/repo/fda/rdkit-master/lib/libRDInchiLib_static.a" )

# Import target "RDGeneral" for configuration "Release"
set_property(TARGET RDGeneral APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(RDGeneral PROPERTIES
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libRDGeneral.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libRDGeneral.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS RDGeneral )
list(APPEND _IMPORT_CHECK_FILES_FOR_RDGeneral "/home/igor/repo/fda/rdkit-master/lib/libRDGeneral.so.1.2015.03.1pre" )

# Import target "RDGeneral_static" for configuration "Release"
set_property(TARGET RDGeneral_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(RDGeneral_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libRDGeneral_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS RDGeneral_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_RDGeneral_static "/home/igor/repo/fda/rdkit-master/lib/libRDGeneral_static.a" )

# Import target "DataStructs" for configuration "Release"
set_property(TARGET DataStructs APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(DataStructs PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "RDGeneral"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDataStructs.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libDataStructs.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS DataStructs )
list(APPEND _IMPORT_CHECK_FILES_FOR_DataStructs "/home/igor/repo/fda/rdkit-master/lib/libDataStructs.so.1.2015.03.1pre" )

# Import target "DataStructs_static" for configuration "Release"
set_property(TARGET DataStructs_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(DataStructs_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDataStructs_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS DataStructs_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_DataStructs_static "/home/igor/repo/fda/rdkit-master/lib/libDataStructs_static.a" )

# Import target "RDGeometryLib" for configuration "Release"
set_property(TARGET RDGeometryLib APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(RDGeometryLib PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "DataStructs;RDGeneral"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libRDGeometryLib.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libRDGeometryLib.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS RDGeometryLib )
list(APPEND _IMPORT_CHECK_FILES_FOR_RDGeometryLib "/home/igor/repo/fda/rdkit-master/lib/libRDGeometryLib.so.1.2015.03.1pre" )

# Import target "RDGeometryLib_static" for configuration "Release"
set_property(TARGET RDGeometryLib_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(RDGeometryLib_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libRDGeometryLib_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS RDGeometryLib_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_RDGeometryLib_static "/home/igor/repo/fda/rdkit-master/lib/libRDGeometryLib_static.a" )

# Import target "Alignment" for configuration "Release"
set_property(TARGET Alignment APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Alignment PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "RDGeometryLib"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libAlignment.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libAlignment.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS Alignment )
list(APPEND _IMPORT_CHECK_FILES_FOR_Alignment "/home/igor/repo/fda/rdkit-master/lib/libAlignment.so.1.2015.03.1pre" )

# Import target "Alignment_static" for configuration "Release"
set_property(TARGET Alignment_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Alignment_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libAlignment_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS Alignment_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_Alignment_static "/home/igor/repo/fda/rdkit-master/lib/libAlignment_static.a" )

# Import target "EigenSolvers" for configuration "Release"
set_property(TARGET EigenSolvers APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(EigenSolvers PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "RDGeneral"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libEigenSolvers.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libEigenSolvers.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS EigenSolvers )
list(APPEND _IMPORT_CHECK_FILES_FOR_EigenSolvers "/home/igor/repo/fda/rdkit-master/lib/libEigenSolvers.so.1.2015.03.1pre" )

# Import target "EigenSolvers_static" for configuration "Release"
set_property(TARGET EigenSolvers_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(EigenSolvers_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libEigenSolvers_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS EigenSolvers_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_EigenSolvers_static "/home/igor/repo/fda/rdkit-master/lib/libEigenSolvers_static.a" )

# Import target "Optimizer" for configuration "Release"
set_property(TARGET Optimizer APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Optimizer PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "RDGeometryLib"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libOptimizer.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libOptimizer.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS Optimizer )
list(APPEND _IMPORT_CHECK_FILES_FOR_Optimizer "/home/igor/repo/fda/rdkit-master/lib/libOptimizer.so.1.2015.03.1pre" )

# Import target "Optimizer_static" for configuration "Release"
set_property(TARGET Optimizer_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Optimizer_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libOptimizer_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS Optimizer_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_Optimizer_static "/home/igor/repo/fda/rdkit-master/lib/libOptimizer_static.a" )

# Import target "ForceField" for configuration "Release"
set_property(TARGET ForceField APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ForceField PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "Optimizer"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libForceField.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libForceField.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS ForceField )
list(APPEND _IMPORT_CHECK_FILES_FOR_ForceField "/home/igor/repo/fda/rdkit-master/lib/libForceField.so.1.2015.03.1pre" )

# Import target "ForceField_static" for configuration "Release"
set_property(TARGET ForceField_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ForceField_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libForceField_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS ForceField_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_ForceField_static "/home/igor/repo/fda/rdkit-master/lib/libForceField_static.a" )

# Import target "DistGeometry" for configuration "Release"
set_property(TARGET DistGeometry APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(DistGeometry PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "EigenSolvers;ForceField"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDistGeometry.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libDistGeometry.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS DistGeometry )
list(APPEND _IMPORT_CHECK_FILES_FOR_DistGeometry "/home/igor/repo/fda/rdkit-master/lib/libDistGeometry.so.1.2015.03.1pre" )

# Import target "DistGeometry_static" for configuration "Release"
set_property(TARGET DistGeometry_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(DistGeometry_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDistGeometry_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS DistGeometry_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_DistGeometry_static "/home/igor/repo/fda/rdkit-master/lib/libDistGeometry_static.a" )

# Import target "Catalogs" for configuration "Release"
set_property(TARGET Catalogs APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Catalogs PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "RDGeneral"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libCatalogs.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libCatalogs.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS Catalogs )
list(APPEND _IMPORT_CHECK_FILES_FOR_Catalogs "/home/igor/repo/fda/rdkit-master/lib/libCatalogs.so.1.2015.03.1pre" )

# Import target "Catalogs_static" for configuration "Release"
set_property(TARGET Catalogs_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Catalogs_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libCatalogs_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS Catalogs_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_Catalogs_static "/home/igor/repo/fda/rdkit-master/lib/libCatalogs_static.a" )

# Import target "GraphMol" for configuration "Release"
set_property(TARGET GraphMol APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(GraphMol PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "RDGeometryLib;RDGeneral;-lpthread"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libGraphMol.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libGraphMol.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS GraphMol )
list(APPEND _IMPORT_CHECK_FILES_FOR_GraphMol "/home/igor/repo/fda/rdkit-master/lib/libGraphMol.so.1.2015.03.1pre" )

# Import target "GraphMol_static" for configuration "Release"
set_property(TARGET GraphMol_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(GraphMol_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libGraphMol_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS GraphMol_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_GraphMol_static "/home/igor/repo/fda/rdkit-master/lib/libGraphMol_static.a" )

# Import target "Depictor" for configuration "Release"
set_property(TARGET Depictor APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Depictor PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "GraphMol"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDepictor.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libDepictor.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS Depictor )
list(APPEND _IMPORT_CHECK_FILES_FOR_Depictor "/home/igor/repo/fda/rdkit-master/lib/libDepictor.so.1.2015.03.1pre" )

# Import target "Depictor_static" for configuration "Release"
set_property(TARGET Depictor_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Depictor_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDepictor_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS Depictor_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_Depictor_static "/home/igor/repo/fda/rdkit-master/lib/libDepictor_static.a" )

# Import target "SmilesParse" for configuration "Release"
set_property(TARGET SmilesParse APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(SmilesParse PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "GraphMol"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libSmilesParse.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libSmilesParse.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS SmilesParse )
list(APPEND _IMPORT_CHECK_FILES_FOR_SmilesParse "/home/igor/repo/fda/rdkit-master/lib/libSmilesParse.so.1.2015.03.1pre" )

# Import target "SmilesParse_static" for configuration "Release"
set_property(TARGET SmilesParse_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(SmilesParse_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libSmilesParse_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS SmilesParse_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_SmilesParse_static "/home/igor/repo/fda/rdkit-master/lib/libSmilesParse_static.a" )

# Import target "FileParsers" for configuration "Release"
set_property(TARGET FileParsers APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(FileParsers PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "SmilesParse;GraphMol"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libFileParsers.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libFileParsers.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS FileParsers )
list(APPEND _IMPORT_CHECK_FILES_FOR_FileParsers "/home/igor/repo/fda/rdkit-master/lib/libFileParsers.so.1.2015.03.1pre" )

# Import target "FileParsers_static" for configuration "Release"
set_property(TARGET FileParsers_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(FileParsers_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libFileParsers_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS FileParsers_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_FileParsers_static "/home/igor/repo/fda/rdkit-master/lib/libFileParsers_static.a" )

# Import target "SubstructMatch" for configuration "Release"
set_property(TARGET SubstructMatch APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(SubstructMatch PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "GraphMol;-lpthread"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libSubstructMatch.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libSubstructMatch.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS SubstructMatch )
list(APPEND _IMPORT_CHECK_FILES_FOR_SubstructMatch "/home/igor/repo/fda/rdkit-master/lib/libSubstructMatch.so.1.2015.03.1pre" )

# Import target "SubstructMatch_static" for configuration "Release"
set_property(TARGET SubstructMatch_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(SubstructMatch_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libSubstructMatch_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS SubstructMatch_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_SubstructMatch_static "/home/igor/repo/fda/rdkit-master/lib/libSubstructMatch_static.a" )

# Import target "ChemReactions" for configuration "Release"
set_property(TARGET ChemReactions APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ChemReactions PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "Descriptors;Fingerprints;DataStructs;Depictor;FileParsers;SubstructMatch;ChemTransforms"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libChemReactions.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libChemReactions.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS ChemReactions )
list(APPEND _IMPORT_CHECK_FILES_FOR_ChemReactions "/home/igor/repo/fda/rdkit-master/lib/libChemReactions.so.1.2015.03.1pre" )

# Import target "ChemReactions_static" for configuration "Release"
set_property(TARGET ChemReactions_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ChemReactions_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libChemReactions_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS ChemReactions_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_ChemReactions_static "/home/igor/repo/fda/rdkit-master/lib/libChemReactions_static.a" )

# Import target "ChemTransforms" for configuration "Release"
set_property(TARGET ChemTransforms APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ChemTransforms PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "SubstructMatch;SmilesParse;-lpthread"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libChemTransforms.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libChemTransforms.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS ChemTransforms )
list(APPEND _IMPORT_CHECK_FILES_FOR_ChemTransforms "/home/igor/repo/fda/rdkit-master/lib/libChemTransforms.so.1.2015.03.1pre" )

# Import target "ChemTransforms_static" for configuration "Release"
set_property(TARGET ChemTransforms_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ChemTransforms_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libChemTransforms_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS ChemTransforms_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_ChemTransforms_static "/home/igor/repo/fda/rdkit-master/lib/libChemTransforms_static.a" )

# Import target "Subgraphs" for configuration "Release"
set_property(TARGET Subgraphs APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Subgraphs PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "GraphMol"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libSubgraphs.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libSubgraphs.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS Subgraphs )
list(APPEND _IMPORT_CHECK_FILES_FOR_Subgraphs "/home/igor/repo/fda/rdkit-master/lib/libSubgraphs.so.1.2015.03.1pre" )

# Import target "Subgraphs_static" for configuration "Release"
set_property(TARGET Subgraphs_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Subgraphs_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libSubgraphs_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS Subgraphs_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_Subgraphs_static "/home/igor/repo/fda/rdkit-master/lib/libSubgraphs_static.a" )

# Import target "FragCatalog" for configuration "Release"
set_property(TARGET FragCatalog APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(FragCatalog PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "Subgraphs;SubstructMatch;SmilesParse;Catalogs;GraphMol;RDGeometryLib;RDGeneral"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libFragCatalog.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libFragCatalog.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS FragCatalog )
list(APPEND _IMPORT_CHECK_FILES_FOR_FragCatalog "/home/igor/repo/fda/rdkit-master/lib/libFragCatalog.so.1.2015.03.1pre" )

# Import target "FragCatalog_static" for configuration "Release"
set_property(TARGET FragCatalog_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(FragCatalog_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libFragCatalog_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS FragCatalog_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_FragCatalog_static "/home/igor/repo/fda/rdkit-master/lib/libFragCatalog_static.a" )

# Import target "Descriptors" for configuration "Release"
set_property(TARGET Descriptors APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Descriptors PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "PartialCharges;SmilesParse;FileParsers;Subgraphs;SubstructMatch;-lpthread"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDescriptors.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libDescriptors.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS Descriptors )
list(APPEND _IMPORT_CHECK_FILES_FOR_Descriptors "/home/igor/repo/fda/rdkit-master/lib/libDescriptors.so.1.2015.03.1pre" )

# Import target "Descriptors_static" for configuration "Release"
set_property(TARGET Descriptors_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Descriptors_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDescriptors_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS Descriptors_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_Descriptors_static "/home/igor/repo/fda/rdkit-master/lib/libDescriptors_static.a" )

# Import target "Fingerprints" for configuration "Release"
set_property(TARGET Fingerprints APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Fingerprints PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "Subgraphs;SubstructMatch;SmilesParse;GraphMol;-lpthread"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libFingerprints.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libFingerprints.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS Fingerprints )
list(APPEND _IMPORT_CHECK_FILES_FOR_Fingerprints "/home/igor/repo/fda/rdkit-master/lib/libFingerprints.so.1.2015.03.1pre" )

# Import target "Fingerprints_static" for configuration "Release"
set_property(TARGET Fingerprints_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(Fingerprints_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libFingerprints_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS Fingerprints_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_Fingerprints_static "/home/igor/repo/fda/rdkit-master/lib/libFingerprints_static.a" )

# Import target "PartialCharges" for configuration "Release"
set_property(TARGET PartialCharges APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(PartialCharges PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "GraphMol"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libPartialCharges.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libPartialCharges.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS PartialCharges )
list(APPEND _IMPORT_CHECK_FILES_FOR_PartialCharges "/home/igor/repo/fda/rdkit-master/lib/libPartialCharges.so.1.2015.03.1pre" )

# Import target "PartialCharges_static" for configuration "Release"
set_property(TARGET PartialCharges_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(PartialCharges_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libPartialCharges_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS PartialCharges_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_PartialCharges_static "/home/igor/repo/fda/rdkit-master/lib/libPartialCharges_static.a" )

# Import target "MolTransforms" for configuration "Release"
set_property(TARGET MolTransforms APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolTransforms PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "GraphMol;EigenSolvers"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolTransforms.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libMolTransforms.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolTransforms )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolTransforms "/home/igor/repo/fda/rdkit-master/lib/libMolTransforms.so.1.2015.03.1pre" )

# Import target "MolTransforms_static" for configuration "Release"
set_property(TARGET MolTransforms_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolTransforms_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolTransforms_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolTransforms_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolTransforms_static "/home/igor/repo/fda/rdkit-master/lib/libMolTransforms_static.a" )

# Import target "ForceFieldHelpers" for configuration "Release"
set_property(TARGET ForceFieldHelpers APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ForceFieldHelpers PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "SmilesParse;SubstructMatch;ForceField"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libForceFieldHelpers.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libForceFieldHelpers.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS ForceFieldHelpers )
list(APPEND _IMPORT_CHECK_FILES_FOR_ForceFieldHelpers "/home/igor/repo/fda/rdkit-master/lib/libForceFieldHelpers.so.1.2015.03.1pre" )

# Import target "ForceFieldHelpers_static" for configuration "Release"
set_property(TARGET ForceFieldHelpers_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ForceFieldHelpers_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libForceFieldHelpers_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS ForceFieldHelpers_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_ForceFieldHelpers_static "/home/igor/repo/fda/rdkit-master/lib/libForceFieldHelpers_static.a" )

# Import target "DistGeomHelpers" for configuration "Release"
set_property(TARGET DistGeomHelpers APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(DistGeomHelpers PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "ForceFieldHelpers;DistGeometry;Alignment;-lpthread"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDistGeomHelpers.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libDistGeomHelpers.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS DistGeomHelpers )
list(APPEND _IMPORT_CHECK_FILES_FOR_DistGeomHelpers "/home/igor/repo/fda/rdkit-master/lib/libDistGeomHelpers.so.1.2015.03.1pre" )

# Import target "DistGeomHelpers_static" for configuration "Release"
set_property(TARGET DistGeomHelpers_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(DistGeomHelpers_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libDistGeomHelpers_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS DistGeomHelpers_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_DistGeomHelpers_static "/home/igor/repo/fda/rdkit-master/lib/libDistGeomHelpers_static.a" )

# Import target "MolAlign" for configuration "Release"
set_property(TARGET MolAlign APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolAlign PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "MolTransforms;SubstructMatch;Alignment"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolAlign.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libMolAlign.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolAlign )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolAlign "/home/igor/repo/fda/rdkit-master/lib/libMolAlign.so.1.2015.03.1pre" )

# Import target "MolAlign_static" for configuration "Release"
set_property(TARGET MolAlign_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolAlign_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolAlign_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolAlign_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolAlign_static "/home/igor/repo/fda/rdkit-master/lib/libMolAlign_static.a" )

# Import target "MolChemicalFeatures" for configuration "Release"
set_property(TARGET MolChemicalFeatures APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolChemicalFeatures PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "SubstructMatch;SmilesParse"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolChemicalFeatures.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libMolChemicalFeatures.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolChemicalFeatures )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolChemicalFeatures "/home/igor/repo/fda/rdkit-master/lib/libMolChemicalFeatures.so.1.2015.03.1pre" )

# Import target "MolChemicalFeatures_static" for configuration "Release"
set_property(TARGET MolChemicalFeatures_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolChemicalFeatures_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolChemicalFeatures_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolChemicalFeatures_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolChemicalFeatures_static "/home/igor/repo/fda/rdkit-master/lib/libMolChemicalFeatures_static.a" )

# Import target "ShapeHelpers" for configuration "Release"
set_property(TARGET ShapeHelpers APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ShapeHelpers PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "MolTransforms"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libShapeHelpers.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libShapeHelpers.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS ShapeHelpers )
list(APPEND _IMPORT_CHECK_FILES_FOR_ShapeHelpers "/home/igor/repo/fda/rdkit-master/lib/libShapeHelpers.so.1.2015.03.1pre" )

# Import target "ShapeHelpers_static" for configuration "Release"
set_property(TARGET ShapeHelpers_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ShapeHelpers_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libShapeHelpers_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS ShapeHelpers_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_ShapeHelpers_static "/home/igor/repo/fda/rdkit-master/lib/libShapeHelpers_static.a" )

# Import target "MolCatalog" for configuration "Release"
set_property(TARGET MolCatalog APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolCatalog PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "GraphMol;Catalogs"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolCatalog.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libMolCatalog.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolCatalog )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolCatalog "/home/igor/repo/fda/rdkit-master/lib/libMolCatalog.so.1.2015.03.1pre" )

# Import target "MolCatalog_static" for configuration "Release"
set_property(TARGET MolCatalog_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolCatalog_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolCatalog_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolCatalog_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolCatalog_static "/home/igor/repo/fda/rdkit-master/lib/libMolCatalog_static.a" )

# Import target "MolDraw2D" for configuration "Release"
set_property(TARGET MolDraw2D APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolDraw2D PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "FileParsers;SmilesParse;Depictor;RDGeometryLib;RDGeneral;SubstructMatch;Subgraphs;GraphMol;RDGeometryLib;-lpthread"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolDraw2D.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libMolDraw2D.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolDraw2D )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolDraw2D "/home/igor/repo/fda/rdkit-master/lib/libMolDraw2D.so.1.2015.03.1pre" )

# Import target "MolDraw2D_static" for configuration "Release"
set_property(TARGET MolDraw2D_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(MolDraw2D_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libMolDraw2D_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS MolDraw2D_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_MolDraw2D_static "/home/igor/repo/fda/rdkit-master/lib/libMolDraw2D_static.a" )

# Import target "FMCS" for configuration "Release"
set_property(TARGET FMCS APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(FMCS PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "Depictor;FileParsers;ChemTransforms;SubstructMatch"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libFMCS.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libFMCS.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS FMCS )
list(APPEND _IMPORT_CHECK_FILES_FOR_FMCS "/home/igor/repo/fda/rdkit-master/lib/libFMCS.so.1.2015.03.1pre" )

# Import target "FMCS_static" for configuration "Release"
set_property(TARGET FMCS_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(FMCS_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libFMCS_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS FMCS_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_FMCS_static "/home/igor/repo/fda/rdkit-master/lib/libFMCS_static.a" )

# Import target "SimDivPickers" for configuration "Release"
set_property(TARGET SimDivPickers APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(SimDivPickers PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "hc;RDGeneral"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libSimDivPickers.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libSimDivPickers.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS SimDivPickers )
list(APPEND _IMPORT_CHECK_FILES_FOR_SimDivPickers "/home/igor/repo/fda/rdkit-master/lib/libSimDivPickers.so.1.2015.03.1pre" )

# Import target "SimDivPickers_static" for configuration "Release"
set_property(TARGET SimDivPickers_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(SimDivPickers_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libSimDivPickers_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS SimDivPickers_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_SimDivPickers_static "/home/igor/repo/fda/rdkit-master/lib/libSimDivPickers_static.a" )

# Import target "hc" for configuration "Release"
set_property(TARGET hc APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(hc PROPERTIES
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libhc.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libhc.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS hc )
list(APPEND _IMPORT_CHECK_FILES_FOR_hc "/home/igor/repo/fda/rdkit-master/lib/libhc.so.1.2015.03.1pre" )

# Import target "hc_static" for configuration "Release"
set_property(TARGET hc_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(hc_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "C"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libhc_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS hc_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_hc_static "/home/igor/repo/fda/rdkit-master/lib/libhc_static.a" )

# Import target "InfoTheory" for configuration "Release"
set_property(TARGET InfoTheory APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(InfoTheory PROPERTIES
  IMPORTED_LINK_INTERFACE_LIBRARIES_RELEASE "DataStructs;RDGeneral"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libInfoTheory.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libInfoTheory.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS InfoTheory )
list(APPEND _IMPORT_CHECK_FILES_FOR_InfoTheory "/home/igor/repo/fda/rdkit-master/lib/libInfoTheory.so.1.2015.03.1pre" )

# Import target "InfoTheory_static" for configuration "Release"
set_property(TARGET InfoTheory_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(InfoTheory_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libInfoTheory_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS InfoTheory_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_InfoTheory_static "/home/igor/repo/fda/rdkit-master/lib/libInfoTheory_static.a" )

# Import target "ChemicalFeatures" for configuration "Release"
set_property(TARGET ChemicalFeatures APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ChemicalFeatures PROPERTIES
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libChemicalFeatures.so.1.2015.03.1pre"
  IMPORTED_SONAME_RELEASE "libChemicalFeatures.so.1"
  )

list(APPEND _IMPORT_CHECK_TARGETS ChemicalFeatures )
list(APPEND _IMPORT_CHECK_FILES_FOR_ChemicalFeatures "/home/igor/repo/fda/rdkit-master/lib/libChemicalFeatures.so.1.2015.03.1pre" )

# Import target "ChemicalFeatures_static" for configuration "Release"
set_property(TARGET ChemicalFeatures_static APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(ChemicalFeatures_static PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_RELEASE "CXX"
  IMPORTED_LOCATION_RELEASE "/home/igor/repo/fda/rdkit-master/lib/libChemicalFeatures_static.a"
  )

list(APPEND _IMPORT_CHECK_TARGETS ChemicalFeatures_static )
list(APPEND _IMPORT_CHECK_FILES_FOR_ChemicalFeatures_static "/home/igor/repo/fda/rdkit-master/lib/libChemicalFeatures_static.a" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
