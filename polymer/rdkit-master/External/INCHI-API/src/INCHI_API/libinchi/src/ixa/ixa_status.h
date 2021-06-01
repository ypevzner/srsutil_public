/*
 * International Chemical Identifier (InChI)
 * Version 1
 * Software version 1.06 pre-release
 * May 19, 2020
 *
 * The InChI library and programs are free software developed under the
 * auspices of the International Union of Pure and Applied Chemistry (IUPAC).
 * Originally developed at NIST.
 * Modifications and additions by IUPAC and the InChI Trust.
 * Some portions of code were developed/changed by external contributors
 * (either contractor or volunteer) which are listed in the file
 * 'External-contributors' included in this distribution.
 *
 * IUPAC/InChI-Trust Licence No.1.0 for the
 * International Chemical Identifier (InChI)
 * Copyright (C) IUPAC and InChI Trust Limited
 *
 * This library is free software; you can redistribute it and/or modify it
 * under the terms of the IUPAC/InChI Trust InChI Licence No.1.0,
 * or any later version.
 *
 * Please note that this library is distributed WITHOUT ANY WARRANTIES
 * whatsoever, whether expressed or implied.
 * See the IUPAC/InChI-Trust InChI Licence No.1.0 for more details.
 *
 * You should have received a copy of the IUPAC/InChI Trust InChI
 * Licence No. 1.0 with this library; if not, please write to:
 *
 * Richard Kidd, InChI Trust,
 * c/o Cambridge Crystallographic Data Centre,
 * 12 Union Road, Cambridge, UK  CB2 1EZ
 *
 * or e-mail to richard@inchi-trust.org
 *
 */


#ifndef __IXA_STATUS_H__
#define __IXA_STATUS_H__

#include "../../../../INCHI_BASE/src/ixa.h"

void STATUS_PushMessage( IXA_STATUS_HANDLE hStatus,
                        IXA_STATUS        vSeverity,
                        char*             pFormat,
                        ... );

#endif
