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

#include <GraphMol/SmilesParse/SmilesParse.h>
#include <GraphMol/Depictor/RDDepictor.h>
#include <GraphMol/FileParsers/FileParsers.h>
#include <GraphMol/FileParsers/MolSupplier.h>
#include <Geometry/Transform3D.h>
#include <GraphMol/MolTransforms/MolTransforms.h>


#include <tclap/CmdLine.h>

#include <fstream>

const int SULPHUR_POSITION = 3;


using namespace std;
void add_coordinates(map<char,RDKit::ROMol*>& m);
void add_coordinates_single(RDKit::RWMol* mol);
void copy_coordinates(RDKit::RWMol* mol, RDKit::ROMol* mol1, RDGeom::Point3D& offset, bool lastmol);
map<char,RDKit::ROMol*> create_map_aa();
map<char,RDKit::ROMol*> create_map_dna();
map<char,RDKit::ROMol*> create_map_rna();
void delete_map(map<char,RDKit::ROMol*>& m);
string mol_to_sdf(RDKit::RWMol *mol);
void parse_disulphide_bond(const string& line, map< pair<int,int>, pair<int,int> >& sp);
void add_disulphide_bonds(vector<RDKit::RWMol*>& molecule, map< pair<int,int>, pair<int,int> >& sp, map< pair<int,int>, int >& sp_atoms, bool v3000);
void add_bond_from_first_to_last(RDKit::RWMol *mol);
vector<string> split(const string &s, char delim);
RDKit::RWMol* reset_moltab(RDKit::ROMol *m, int first, int last);
pair<RDKit::ROMol*,bool> rearrange_mol(RDKit::ROMol* nmol, vector<RDKit::ROMol*>& patterns);
void delete_modified(map<pair<int,int>,pair<RDKit::ROMol*,bool> >& modified_mols);
void create_smarts_patterns_aa(vector<RDKit::ROMol*>& patterns);
void create_smarts_patterns_dna(vector<RDKit::ROMol*>& patterns);
void create_smarts_patterns_rna(vector<RDKit::ROMol*>& patterns);
void delete_smarts_patterns(vector<RDKit::ROMol*>& patterns);
RDKit::RWMol* process_seq(const string& seq, const map<char,RDKit::ROMol*>& a2smi, int num, map< pair<int,int>, pair<int,int> >& sp, map< pair<int,int>,int >& sp_atoms, 
			  map<pair<int,int>,pair<RDKit::ROMol*,bool> >& modified_mols, bool v3000);
string mol_to_sdf(RDKit::RWMol *mol);
void add_bond_from_first_to_last(RDKit::RWMol *mol);
string trim(const std::string& s,const std::string& delimiters = " \f\n\r\t\v" );

pair<int,int> get_attachment_points_na(RDKit::ROMol* m);
void flip_sugar_up(RDKit::ROMol *mol);
RDKit::ROMol* rearrange_residue(RDKit::ROMol* nmol);
string mol_to_sdf_na(RDKit::RWMol *mol);
void create_map_na(map<char,RDKit::ROMol*> &m);
void prepare_residues(vector<RDKit::ROMol*>  &sugars,vector<RDKit::ROMol*>  &linkers, const vector<RDKit::ROMol*> &modified_mols, const int num_seq, const int length,
		      RDKit::ROMol* std_sugar, RDKit::ROMol* std_linkage, RDKit::ROMol* std_sugar_first);
void copy_coordinates_na(RDKit::RWMol* mol, RDKit::ROMol* mol1, RDGeom::Point3D& offset, int oldsize, int beginning, int ending, int base);
RDKit::RWMol* process_na(const string &line,const map<char,RDKit::ROMol*> &a2smi,vector<RDKit::ROMol*>  &sugars,vector<RDKit::ROMol*>  &linkers, bool v3000);
void delete_residues(vector<RDKit::ROMol*>  &res);
void fix_moltab(string &moltab);
string correct_attachment_point(const string &p, RDKit::ROMol *mol);
