{**********************************************************************
    $Id: ImapCopy.dpr
    This file is part of imapcopy
    Copyright (c) 2001 Armin Diehl

    IMapCopy main for Delphi

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 **********************************************************************}

PROGRAM IMapCopy;

USES ImapTools, ImapCopyMain;

BEGIN
  ImapCopyMain.Header;
  ImapCopyMain.ParseCommandLine;
  ImapCopyMain.Main;
END.
