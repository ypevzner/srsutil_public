RDBASE =  /home/igor/repo/fda/rdkit-master/
BOOST_HOME = /home/igor/repo/fda/boost_1_55_0/
TCLAP = /home/igor/repo/fda/tclap-1.2.1/include
CPPFLAGS = -g -O2 -I$(RDBASE) -I$(RDBASE)/Code -I$(BOOST_HOME) -I$(TCLAP)
LDFLAGS = -g -O2  -static -L$(RDBASE)/lib  

LIBS = -lRDInchiLib_static -lInchi_static -lChemReactions_static -lFileParsers_static -lSmilesParse_static -lDepictor_static -lSubstructMatch_static -lGraphMol_static -lDataStructs_static -lRDGeometryLib_static -lRDGeneral_static -lChemTransforms_static -lMolTransforms_static -lEigenSolvers_static
#LIBS = -lRDInchiLib -lInchi -lChemReactions -lFileParsers -lSmilesParse -lDepictor -lSubstructMatch -lGraphMol -lDataStructs -lRDGeometryLib -lRDGeneral -lChemTransforms -lMolTransforms -lEigenSolvers

all: modna srs2mol polymer

srs2mol: srs2mol.o nucleic_acids.o seq2mol.o aa.o coords.o disulfide.o modified.o pugixml.o fix.o
	g++ -o $@ $^ $(LDFLAGS) $(LIBS)

modna:	modna.o nucleic_acids.o aa.o modified.o coords.o disulfide.o
	g++ -o $@ $^ $(LDFLAGS) $(LIBS)

polymer: polymer.o pugixml.o
	g++ -o $@ $^ $(LDFLAGS) $(LIBS)

mol2inchi: mol2inchi.o
	g++ -o $@ $^ $(LDFLAGS) $(LIBS)

%.o: %.cpp 
	g++ -c -o $@ $< $(CPPFLAGS)

.PHONY: clean

clean:
	rm -f *.o *~ core *.exe

