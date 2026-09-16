{**********************************************************************
    $Id: ImapCopy.pp
    This file is part of imapcopy
    Copyright (c) 2001..2004 Armin Diehl

    IMapCopy main for FreePascal

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 **********************************************************************}


PROGRAM imapcopy;

{$MODE Delphi}

USES imapcopymain;

BEGIN
  imapcopymain.Header;
  imapcopymain.ParseCommandLine;
  imapcopymain.Main;
END.
