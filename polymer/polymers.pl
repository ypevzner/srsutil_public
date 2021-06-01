#!/usr/bin/perl
use XML::LibXML::Reader;
use HTML::Entities;

$INPUT = $ARGV[0];
$OUT_DIR = $ARGV[1];

my $id;
my $body;

my $reader = XML::LibXML::Reader->new(location => $INPUT)  or die "cannot read file.xml\n";
while ($reader->read) 
{   
    if ($reader->localName() eq "DATA_RECORD")
    {
	if ($body)
	{
	    create_file($id, decode_entities($body));
	}
	undef $body;
	undef $id;
    }
    if ($reader->localName() eq "SUBSTANCE_ID")
    {
	my $content = $reader->readInnerXml();
	$id = $id.$content;
    }
    if ($reader->localName() eq "DESC_PART1")
    {
	$body = $body.$reader->readInnerXml();
    }	
    if ($reader->localName() eq "DESC_PART2")
    {
	$body = $body.$reader->readInnerXml();
    }	
    if ($reader->localName() eq "DESC_PART3")
    {
	$body = $body.$reader->readInnerXml();
    }
   
}

if ($body)
{
    create_file($id,decode_entities($body));
}
undef $body;
$reader->finish();

sub create_file($$)
{
    my $id = shift;
    my $body = shift;
    open(OUT,">".$OUT_DIR."/".$id.".xml");
    print OUT $body;
    close(OUT);
}
