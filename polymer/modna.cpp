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

#include "seq2mol.h"

using namespace std;


int main(int argc, char *argv[])
{
  string input,output,modified;
  bool v3000 = false;
  try
    {
      TCLAP::CmdLine cmd("Modified nucleotide sequence converter to MDL MOL", ' ', "1.0");
      TCLAP::UnlabeledValueArg<string>  input_arg( "sequence", "Input file with sequences", true, "", "input.seq"  );
      cmd.add( input_arg );
      TCLAP::UnlabeledValueArg<string> modified_arg("residue","File with modified sugar and linkage residues",true,"","residues.sdf");
      cmd.add( modified_arg );
      TCLAP::UnlabeledValueArg<string>  output_arg( "output", "Output SD file", true, "", "output.sdf"  );
      cmd.add( output_arg );

      cmd.parse( argc, argv );
      input = input_arg.getValue();
      output = output_arg.getValue();
      modified = modified_arg.getValue();

    }  catch (TCLAP::ArgException &e)  // catch any exceptions
    { cerr << "error: " << e.error() << " for arg " << e.argId() << endl; }

  boost::logging::disable_logs("rdApp.*"); 

  // Preparing input stream
  ifstream in;
  in.open(input.c_str());
  if (!in.is_open())
    {
      cerr<<"Cannot open input file: "<<input<<endl;
      return 2;
    }

  // Preparing output stream
  ofstream out;
  out.open(output.c_str());
  if (!out.is_open())
    {
      cerr<<"Cannot open output file: "<<output<<endl;
      return 3;
    }
 
 
  vector<RDKit::ROMol*> modified_mols;
  if (!modified.empty())
    {
      RDKit::SDMolSupplier sdsup(modified);
      while (!sdsup.atEnd()) 
	{
	  RDKit::ROMol *nmol = sdsup.next();
	  if (nmol) 
	    {
	      modified_mols.push_back(rearrange_residue(nmol));
	    }
	}
    }
  RDKit::ROMol* std_linkage = rearrange_residue(RDKit::SmilesToMol("[3*]P(O)(=O)[4*]"));  
  RDKit::ROMol* std_sugar = rearrange_residue(RDKit::SmilesToMol("[3*]OC[C@H]1O[C@@H]([4*])[C@H](O)[C@@H]1O[5*]"));
  RDKit::ROMol* std_sugar_first = rearrange_residue(RDKit::SmilesToMol("OP(O)(=O)OC[C@H]1O[C@@H]([4*])[C@H](O)[C@@H]1O[5*]"));

  // Create molecules
  map<char,RDKit::ROMol*> a2smi;
  create_map_na(a2smi);
    
  vector<RDKit::RWMol*> molecule;
  int num_seq = 0;
  while (in.good())
    {
      string line;
      getline(in,line);
      if (!line.empty())
	{
	  if (line.at(line.length()-1) == '\r') line.erase(line.length()-1);
	  vector<RDKit::ROMol*> sugars,linkers;
	  prepare_residues(sugars,linkers,modified_mols,num_seq,line.length(),std_sugar,std_linkage,std_sugar_first);
	  RDKit::RWMol* mol = process_na(line,a2smi,sugars,linkers,v3000);
	  if (mol != NULL)
	    molecule.push_back(mol);
	}
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
  in.close();
  out.close();
  return 0;
}
