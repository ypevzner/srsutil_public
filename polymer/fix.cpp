#include <fstream>
#include <string>
#include <iostream>
#include <sstream>
#include <list>
#include <vector>
#include <algorithm> 
#include <string.h>


void fix_chunks(const std::string &line, std::vector<std::string> &chunks, int &i, int num_chunk, int chunk_length)
{
  while (isspace(line[i]))
    i--;
  int k = 1;
  while (i>=0 && num_chunk > 0)
    {	   
      if ((isspace(line[i]) && k != 0) || k == chunk_length)
	{
	  std::string chunk = line.substr(i,k);		
	  for (int j=0; j<chunk_length-k; j++)
	    chunk = " "+chunk;
	  chunks.push_back(chunk);
	  k = 0;
	  if (isspace(line[i]))
	    {
	      while (isspace(line[i]))
		i--;
	      i++;
	    }
	  num_chunk--;
	}
      i--;
      k++;
    }
  std::reverse(chunks.begin(),chunks.end());
}

void fix_chunks_and_add(const std::string &line, int &i, int num_chunk, int chunk_length, std::string &fixed)
{
  std::vector<std::string> chunks;
  fix_chunks(line, chunks, i, num_chunk, chunk_length);
  for (std::vector<std::string>::const_reverse_iterator s = chunks.rbegin(); s != chunks.rend(); ++s)
    fixed = *s + fixed;
}

void fix_moltab(std::string &moltab)
{
  std::stringstream infile(moltab);  

  std::string line;
  std::list<std::string> header;
  std::string fixed;
  int num_atoms = 0;
  int num_bonds = 0;
  while (std::getline(infile, line))
    {
      size_t length = line.length();
      if (length > strlen("V2000") && line.substr(length-strlen("V2000")) == "V2000")
	{
	  int i = length - 7;
	  fixed = line.substr(length - 6);
	  std::vector<std::string> chunks;
	  fix_chunks(line, chunks, i, 11, 3);	
	  if (!chunks.empty())
	    {
	      std::istringstream(chunks[0]) >> num_atoms;
	      std::istringstream(chunks[1]) >> num_bonds;
	    }
	  for (std::vector<std::string>::const_reverse_iterator s = chunks.rbegin(); s != chunks.rend(); ++s)
	    {
	      fixed = *s + fixed;
	    }
	  break;
	}
      else 
	{
	  header.push_back(line);
	  if (header.size() > 3)
	    header.pop_front();
	}
    }

  if (num_atoms > 0 && num_bonds > 0)
    {
      int header_size = header.size();
      for (int j=0; j<3-header_size; j++)
	{
	  header.push_front("");
	}

      std::vector<std::string> body(header.begin(),header.end());
      body.push_back(fixed);
      int n = 0;
      while (n < num_atoms && std::getline(infile, line))
	{
	  int i = line.length() - 1;
	  fixed.clear();

	  fix_chunks_and_add(line, i, 11, 3, fixed);
	  fix_chunks_and_add(line, i, 1, 2, fixed);

	  while (isspace(line[i])) i--;
	  std::string atom;
	  while (!isspace(line[i]))
	    {
	      atom = line[i]+atom;
	      i--;
	    }
	  while (atom.size() < 3)
	    atom += " ";

	  fixed = atom + fixed;

	  i--;
	  fixed = " "+fixed;

	  fix_chunks_and_add(line, i, 3, 10, fixed);
	  
	  body.push_back(fixed);
	  n++;
	}

      n = 0;
      while (n < num_bonds && std::getline(infile, line))
	{
	  int i = line.length() - 1;
	  fixed.clear();
	  fix_chunks_and_add(line, i, 7, 3, fixed);

	  body.push_back(fixed);
	  n++;
	}

      while (std::getline(infile, line) )
	{
	  std::string fixed = line;
	  if (line.length() > 4 && line[0] == 'M' && isspace(line[1]))
	    {
	      int i=1; 
	      while (isspace(line[i]) && i < line.length())
		i++;	      
	      fixed = line.substr(0,1)+"  "+ line.substr(i,3);
	      i += 3;
	      int desired_length = 3;
	      do
		{
		  while (isspace(line[i]) && i < line.length())
		    i++;
		  int j = i;
		  while (!isspace(line[j]) && j-i < 3 && j < line.length())
		    j++;
		  int length = j - i;
		  if (length > 0)
		    {
		      std::string chunk = line.substr(i,length);
		      for (int k=0; k<desired_length - length; k++)
			chunk = " "+chunk;
		      fixed += chunk;
		    }
		  desired_length = 4;
		  i = j;
		} while (i < line.length());
	    }
	  
	  body.push_back(fixed); 
	  if (fixed == "M  END")
	    break;
	  /*
	    std::string start = fixed.substr(0,6);
	    if (start[0] == 'M' && start != "M  CHG" && start != "M  RAD" && start != "M  ISO")
	    {
	      std::cerr << "Not a regular molecule in moltab " << start << std::endl;
	      exit(1);
	    }
	  */
	}

      moltab.clear();
      for (int i=0; i<body.size(); i++)
	moltab += body[i]+"\n";
    }
}

/*
int main(int argc, char **argv)
{
  std::ifstream in(argv[1]);
  std::string s((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
  fix_moltab(s);
  std::cout << s;
}
*/
	
