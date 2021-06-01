/*
written by Igor Filippov, VIF Innovations, LLC, 2013

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#include <cstdlib>
#include <External/INCHI-API/inchi.h>
#include "pugixml.hpp"
#include "seq2mol.h"

using namespace std;

void traverse_group(pugi::xml_node group, const string& note)
{
  for (; group; group = group.next_sibling())
    if (!group.empty())
      {
	string val = group.child_value();
	if (!val.empty())
	  {
	    val = trim(val);
	    if (!val.empty() && val != "0" && val != "0;")
	      {
		cerr << note << ": " << val << endl;
		exit(1);
	      }
	  }
	pugi::xml_node  subgroup = group.first_child();
	traverse_group(subgroup,note);
      }
}

void add_connectivity(RDKit::ROMol *mol, string &connectivity)
{
  if (!connectivity.empty()) 
    {
      vector<string> links1 = split(connectivity,';');
      vector<string> links;
      for (vector<string>::iterator p=links1.begin(); p != links1.end(); ++p)
	if (!p->empty())
	  {
	    string pos = trim(*p);
	    if (pos != "0")
	      {
		links.push_back(correct_attachment_point(pos,mol));
	      }
	  }
      
      if (links.size() == 1)
	mol->setProp("Attachment_Points",links.front());
      else if (links.size() == 2)
	mol->setProp("Attachment_Points",links.front()+" "+links.back());	
    }
}

void populate_modified_mols(RDKit::ROMol *mol, vector<RDKit::ROMol*> &patterns, string &residue_site, map<pair<int,int>,pair<RDKit::ROMol*,bool> > &modified_mols)
{
  RDDepict::compute2DCoords(*mol);
  pair<RDKit::ROMol*,bool> mol_pair = rearrange_mol(mol,patterns);
  vector<string> links = split(residue_site,';');
  for (int i=0; i<links.size(); i++)
    {
      string line = trim(links[i]);
      if (!line.empty())
	{
	  vector<string> seq_pos = split(line,'_');
	  if (seq_pos.size() != 2 || seq_pos[0].empty() || seq_pos[1].empty())
	    {
	      cerr << "Malformed residue site line entry: " << line << endl;
	      exit(1);
	    }
	  int seq = atoi(seq_pos[0].c_str())-1;
	  int pos = atoi(seq_pos[1].c_str())-1;
	  if (pos < 0 || seq < 0)
	    {
	      cerr << "Modified position or sequence did not start at 1: "<<line<<endl;
	      exit(1);
	    }
	  else
	    modified_mols[make_pair(seq,pos)] = mol_pair;
	}
    }
}

bool get_modifications(pugi::xml_node &protein, const string &external_moltab, map<string,RDKit::ROMol *> &name_to_mol, vector<RDKit::ROMol*> &patterns, map<pair<int,int>,pair<RDKit::ROMol*,bool> > &modified_mols)
{
  bool external_is_modification = false;
  for (pugi::xml_node modgroup = protein.child("MODIFICATION_GROUP"); modgroup; modgroup = modgroup.next_sibling("MODIFICATION_GROUP"))
    if (!modgroup.empty())
      {

	for (pugi::xml_node physgroup = modgroup.child("PHYSICAL_MODIFICATION_GROUP"); physgroup; physgroup = physgroup.next_sibling("PHYSICAL_MODIFICATION_GROUP"))
	  if (!physgroup.empty())
	    traverse_group(physgroup.first_child(),"Physical modification group not empty");
	
	for (pugi::xml_node agentgroup = modgroup.child("AGENT_MODIFICATION_GROUP"); agentgroup; agentgroup = agentgroup.next_sibling("AGENT_MODIFICATION_GROUP"))
	  if (!agentgroup.empty())
	    traverse_group(agentgroup.first_child(),"Agent modification group not empty");


	for (pugi::xml_node structgroup = modgroup.child("STRUCTURAL_MODIFICATION_GROUP"); structgroup; structgroup = structgroup.next_sibling("STRUCTURAL_MODIFICATION_GROUP"))
	  if (!structgroup.empty())
	  {

	    pugi::xml_node  amount = structgroup.child("MOLECULAR_FRAGMENT_MOIETY").child("AMOUNT");
	    if (!amount.empty())
	      traverse_group(amount.first_child(),"Amount not empty at structural modification group");
	    string role = structgroup.child("MOLECULAR_FRAGMENT_MOIETY").child_value("ROLE");
	    if (!role.empty())
	      {
		role = trim(role);
		if (!role.empty())
		  {
		    std::transform(role.begin(), role.end(), role.begin(), ::toupper);
		    if (role != "AMINO ACID SUBSTITUTION")
		      { 
			cerr << "Role is not amino acid substitution: " << role << endl;
			exit(1);
		      }
		  }
	      }
	    string residue_site = structgroup.child_value("RESIDUE_SITE");
	    if (!residue_site.empty())
	      {
		if (role.empty())
		  {
		    cerr << "Role is not amino acid substitution: "<< role << endl;
		    exit(1);
		  }
		string inchi = structgroup.child("MOLECULAR_FRAGMENT_MOIETY").child_value("MOLECULAR_FRAGMENT_INCHI");
		string connectivity1 = structgroup.child("MOLECULAR_FRAGMENT_MOIETY").child_value("FRAGMENT_CONNECTIVTY");
		string connectivity2 = structgroup.child("MOLECULAR_FRAGMENT_MOIETY").child_value("FRAGMENT_CONNECTIVITY");
		string connectivity = connectivity1.empty() ? connectivity2 : connectivity1;
		string name = structgroup.child("MOLECULAR_FRAGMENT_MOIETY").child_value("MOLECULAR_FRAGMENT_NAME");
		string moltab = structgroup.child("MOLECULAR_FRAGMENT_MOIETY").child_value("MOLFILE");
		RDKit::ROMol *mol = NULL; 
		if (structgroup.child("MOLECULAR_FRAGMENT_MOIETY").child("MOLFILE").attribute("external").as_bool())
		  {
		    external_is_modification = true;
		    if (!external_moltab.empty())
		      {
		
			moltab = external_moltab;
		      }
		    else
		      {
			cerr << "External molfile not specified" << endl;
			exit(1);
		      }
		  }
		
		if (!mol && !moltab.empty())
		  {
		    fix_moltab(moltab);
		    try
		      {
			mol = RDKit::MolBlockToMol(moltab,true,false,false);
		      }
		    catch(...) {mol = NULL;}
		  }
		if (!mol && !name.empty())
		  {
		    std::transform(name.begin(), name.end(), name.begin(), ::tolower);		    
		    name = trim(name);
		    if (name_to_mol.find(name) != name_to_mol.end())
		      {
			mol = new RDKit::ROMol(*name_to_mol[name]);
			if (connectivity.empty() && mol && mol->hasProp("CONNECTORS"))
			  mol->getProp("CONNECTORS",connectivity);		   
		      }
		  }

		if (!mol && !inchi.empty())
		  {
		    RDKit::ExtraInchiReturnValues rv;
		    try {
		      mol = RDKit::InchiToMol(inchi,rv); 
		    } catch(...) {mol = NULL;}
		  }

		if (mol)
		  {	
		    RDKit::ExtraInchiReturnValues rv;
		    std::string inchi;
		    try
		      {
			inchi = RDKit::MolToInchi(*mol,rv, "/LargeMolecules");
		      } catch(...) {}
		    if (inchi.empty())
		      {
			cerr << "Modification is not InChI-able" << endl;
			exit(1);
		      }
	    		 
		   
		    add_connectivity(mol, connectivity);
		    populate_modified_mols(mol, patterns, residue_site, modified_mols);
		  }
		else
		  {
		    cerr << "Modified residue cannot be read " << name << endl;
		    exit(1);
		  }
	      }       
	  }
      }
  return external_is_modification;
}


void compare_inchis(bool external_is_modification, const string &external_moltab, const vector<RDKit::RWMol*> &molecule)
{
  if (!external_is_modification && !external_moltab.empty())
    {
      std::string inchi1;
      std::string inchi2;
      string moltab = external_moltab;
      //fix_moltab(moltab);
      RDKit::ROMol *mol = NULL; 
      try
	{
	  mol = RDKit::MolBlockToMol(moltab,true,false,false);
	}
      catch(...) {mol = NULL;}
      if (mol)
	{
	  RDKit::ExtraInchiReturnValues rv1;
	  try
	    {
	      inchi1 = RDKit::MolToInchi(*mol,rv1, "/LargeMolecules");
	    } catch(...) {}
	  delete mol;
	}
      if ( !molecule.empty() && molecule.front())
	{
	  RDKit::ExtraInchiReturnValues rv2;
	  try
	    {
	      inchi2 = RDKit::MolToInchi(*molecule.front(),rv2, "/LargeMolecules");
	    } catch(...) {}	 
	}
      
      if (inchi1 != inchi2 || (inchi1.empty() && inchi2.empty()))
	{
	  cerr << "Original and generated InChI do not match" << endl;
	  exit(1);
	}
    }
}

void get_registry(const string &in, map<string,RDKit::ROMol *> &name_to_mol)
{
  if (in.empty())
    return;
  RDKit::SDMolSupplier sdsup(in,true,false,false);
  while (!sdsup.atEnd())
    {
      RDKit::ROMol *mol = sdsup.next();
      if (mol)
	{
	  if (mol->hasProp("NAME"))
	    {
	      string name;
	      mol->getProp("NAME",name);
	      std::transform(name.begin(), name.end(), name.begin(), ::tolower);
	      name = trim(name);
	      if (!name.empty())
		name_to_mol[name] = new RDKit::ROMol(*mol);
	    }
	   if (mol->hasProp("SYNONYMS"))
	     {
	       string synonyms;
	       mol->getProp("SYNONYMS",synonyms);
	       vector<string> names = split(synonyms,'\n');    
	       for (int i=0; i<names.size(); i++)
		 if (!names[i].empty())
		   {
		     string name = trim(names[i]);
		     if (!name.empty())
		       {
			 std::transform(name.begin(), name.end(), name.begin(), ::tolower);
			 name_to_mol[name] = new RDKit::ROMol(*mol);
		       }
		   }

	     }
	   delete mol;
	}
    }
}

void process_proteins(pugi::xml_node &protein, ofstream &out, bool v3000, const string &external_name, const string &registry_name)
{
  map<string,RDKit::ROMol *> name_to_mol;
  get_registry(registry_name, name_to_mol);
  string external_moltab;
  if (!external_name.empty())
    {
      std::ifstream in(external_name.c_str());
      std::string s((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
      external_moltab = s;
    }
  
  vector<string> sequences; 
  for (pugi::xml_node subunit = protein.child("SUBUNIT_GROUP"); subunit; subunit = subunit.next_sibling("SUBUNIT_GROUP"))
    if (!subunit.empty())
      {
	std::string seq = subunit.child_value("SEQUENCE");
	if (!seq.empty())
	  sequences.push_back(seq);
      }
  if (sequences.empty())
    {
      cerr << "No sequence found" << endl;
      exit(1);
    }

  string n_glycosylatoin = protein.child("GLYCOSYLATION").child_value("N_GLYCOSYLATION");
  string o_glycosylatoin = protein.child("GLYCOSYLATION").child_value("O_GLYCOSYLATION");
  string c_glycosylatoin = protein.child("GLYCOSYLATION").child_value("C_GLYCOSYLATION");
  if (!n_glycosylatoin.empty() || !o_glycosylatoin.empty() || !c_glycosylatoin.empty())
    {
      cerr <<"Glycosylation found, stopping processing" << endl;
      exit(1);
    }


  // Load disulphide bonds
  map< pair<int,int>, pair<int,int> > sp;
  string disulfide_linkage = protein.child_value("DISULFIDE_LINKAGE");
  if (!disulfide_linkage.empty()) // disulphide bonds are given
    {
      vector<string> links = split(trim(disulfide_linkage),';');
     
      for (int i=0; i<links.size(); i++)
	if (!links[i].empty())
	{
	  string line = trim(links[i]);
	  if (!line.empty())
	    {
	      parse_disulphide_bond(line,sp);
	    }
	}
    }

  //  Load modified amino acids
  vector<RDKit::ROMol*> patterns;
  create_smarts_patterns_aa(patterns);

 // modifications protein/[modification_group]/structural_modification_group/residue_site
  map<pair<int,int>,pair<RDKit::ROMol*,bool> > modified_mols;
  bool external_is_modification = get_modifications(protein, external_moltab, name_to_mol, patterns, modified_mols);

  // Create molecules
  map<char,RDKit::ROMol*> a2smi;
  a2smi = create_map_aa();
    
  vector<RDKit::RWMol*> molecule;
  map< pair<int,int>, int > sp_atoms;
  for (int i=0; i<sequences.size(); i++)
    {
      string line = sequences[i];
      if (!line.empty())
	{
	  RDKit::RWMol* mol = process_seq(trim(line),a2smi,i,sp,sp_atoms, modified_mols, v3000);
	  if (mol != NULL)
	    molecule.push_back(mol);
	}
    }

  // Add disulphide bonds
  if (!sp_atoms.empty())
    add_disulphide_bonds(molecule,sp,sp_atoms,v3000);

  // Output molecules

  compare_inchis(external_is_modification, external_moltab, molecule);

  for (int i=0; i<molecule.size(); i++)
    if (molecule[i] != NULL)
      {
	//if (circular)
	// add_bond_from_first_to_last(molecule[i]);

	string moltable = mol_to_sdf(molecule[i]);
	if (!moltable.empty())
	  {
	    out << moltable;
	    out<<"$$$$"<<endl;
	  }
      }

  // Closing out
  delete_map(a2smi);
  delete_modified(modified_mols);
  delete_smarts_patterns(patterns);
  for (map<string,RDKit::ROMol *>::iterator it=name_to_mol.begin(); it != name_to_mol.end(); ++it)
    if (it->second)
      {
	delete it->second;
	it->second = NULL;
      }
  name_to_mol.clear();
  
}

void process_nucleic_acid(pugi::xml_node &nucleic_acid, ofstream &out, bool v3000)
{
  cerr << "Current version does not support nucleic acid processing" << endl;
  exit(1);
  vector<RDKit::ROMol*> modified_mols; // TODO
  vector<string> sequences; 
  for (pugi::xml_node subunit = nucleic_acid.child("SUBUNIT_GROUP"); subunit; subunit = subunit.next_sibling("SUBUNIT_GROUP"))
    if (!subunit.empty())
      {
	std::string seq = subunit.child_value("BASE_SEQUENCE");
	if (!seq.empty())
	  sequences.push_back(seq);
      }
  if (sequences.empty())
    {
      cerr << "No sequence found" << endl;
      exit(1);
    }

  RDKit::ROMol* std_linkage = rearrange_residue(RDKit::SmilesToMol("[3*]P(O)(=O)[4*]"));  
  RDKit::ROMol* std_sugar = rearrange_residue(RDKit::SmilesToMol("[3*]OC[C@H]1O[C@@H]([4*])[C@H](O)[C@@H]1O[5*]"));
  RDKit::ROMol* std_sugar_first = rearrange_residue(RDKit::SmilesToMol("OP(O)(=O)OC[C@H]1O[C@@H]([4*])[C@H](O)[C@@H]1O[5*]"));

  // Create molecules
  map<char,RDKit::ROMol*> a2smi;
  create_map_na(a2smi);
    
  vector<RDKit::RWMol*> molecule;
  for (unsigned int num_seq = 0; num_seq < sequences.size(); num_seq++)
    {
      string line = sequences[num_seq];
      vector<RDKit::ROMol*> sugars,linkers;
      prepare_residues(sugars,linkers,modified_mols,num_seq,line.length(),std_sugar,std_linkage,std_sugar_first);
      RDKit::RWMol* mol = process_na(line,a2smi,sugars,linkers,v3000);
      if (mol != NULL)
	molecule.push_back(mol);
      num_seq++;
    }

  // Output molecules
  for (int i=0; i<molecule.size(); i++)
    if (molecule[i] != NULL)
      {
	string moltable = mol_to_sdf_na(molecule[i]);
	if (!moltable.empty())
	  {
	    out << moltable;
	    out<<"$$$$"<<endl;
	  }
      }

  // Closing out
  delete_map(a2smi);
  delete_residues(modified_mols);
  delete(std_linkage);
  delete(std_sugar);
  delete(std_sugar_first);
}

int main(int argc, char *argv[])
{
  string input,output,external_name,registry_name;
  bool circular = false; // TODO
  bool v3000 = true;
  try
    {
      TCLAP::CmdLine cmd("SRS XML converter to MDL MOL", ' ', "1.0");
      TCLAP::UnlabeledValueArg<string>  input_arg( "input.xml", "Input XML file", true, "", "filename"  );
      cmd.add( input_arg );
      TCLAP::UnlabeledValueArg<string>  output_arg( "output.sdf", "Output SD file", true, "", "filename"  );
      cmd.add( output_arg );
      //TCLAP::SwitchArg v3000_arg("","v3000","Output MOL V3000 format when molecules is too big"); 
      //cmd.add( v3000_arg );
      TCLAP::ValueArg<std::string> external_arg("","external","external molfile name",false,"","filename");
      cmd.add( external_arg );
      TCLAP::ValueArg<std::string> registry_arg("","registry","registry SDF name",false,"","filename");
      cmd.add( registry_arg );

      cmd.parse( argc, argv );
      input = input_arg.getValue();
      output = output_arg.getValue();
      //v3000 = v3000_arg.getValue();
      external_name = external_arg.getValue();
      registry_name = registry_arg.getValue();
    }  catch (TCLAP::ArgException &e)  // catch any exceptions
    { cerr << "error: " << e.error() << " for arg " << e.argId() << endl; exit(1);}

  boost::logging::disable_logs("rdApp.*");

  // Preparing output stream
  ofstream out;
  out.open(output.c_str());
  if (!out.is_open())
    {
      cerr<<"Cannot open output file: "<<output<<endl;
      exit(1);
    }

  pugi::xml_document doc;
  if (!doc.load_file(input.c_str()))
    {
      cerr<<"Cannot parse input file: "<<input<<endl;
      exit(1);
    }
  pugi::xml_node protein;
  // Seems to be 2 predominant formats
  pugi::xml_node protein1 = doc.child("SUBSTANCE").child("SINGLE_SUBSTANCE").child("ELEMENT_TYPE").child("PROTEIN");
  pugi::xml_node protein2 = doc.child("PROTEIN");

  pugi::xml_node nucleic_acid = doc.child("SUBSTANCE").child("SINGLE_SUBSTANCE").child("ELEMENT_TYPE").child("NUCLEIC_ACID");


  if (!protein1.empty())
    protein = protein1;
  else if (!protein2.empty())
    protein = protein2;


  if (protein.empty() && nucleic_acid.empty())
    {
      cerr << "Cannot find neither protein nor nucleic acid part of XML input" << endl;
      exit(1);
    }

  if (!protein.empty())
    process_proteins(protein,out,v3000,external_name,registry_name);
  
  if (!nucleic_acid.empty())
    process_nucleic_acid(nucleic_acid,out,v3000);
 
 

  out.close();
  return 0;
}
