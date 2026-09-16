{**********************************************************************
    $Id: logfile.pas
    This file is part of imapcopy
    Copyright (c) 2001-2005 Armin Diehl

    Logging to screen or file for imapcopy

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

 **********************************************************************}

unit logfile;

{$IFDEF FPC}
  {$MODE Delphi}
{$ENDIF}

interface

{$I-}

uses sysutils;

const
  log_message =  0;
  log_warning =  1;
  log_error   =  2;
  log_status  =  4;
  log_fatal   =  8;
  log_file    = 16;

  log_to_stdout  = log_message + log_status;
  log_to_stderr  = log_warning + log_error + log_fatal;
  log_to_logfile = log_warning + log_error + log_status + log_fatal + log_file;

  log_to_file    : boolean = false;
  log_filename   : string  = 'imapcopy.log';
  log_file_open  : boolean = false;

function log_openfile (const fn : string) : boolean;
function log_closefile : boolean;
procedure log (logclass : integer; msg : string);
procedure logfmt (logclass : integer; fmtstr : string; args : array of const);


implementation

var fLog : file;

function log_openfile (const fn : string) : boolean;
begin
  log_closefile;
  if fn <> '' then
    log_filename := fn;
  assign (fLog,log_filename);
  reset (fLog,1);
  if IOResult <> 0 then
    rewrite (fLog,1)
  else
    seek (fLog, filesize(fLog));
  log_file_open := (IOResult = 0);
  result := log_file_open;
end;

function log_closefile : boolean;
begin
  if log_file_open then
  begin
    close (fLog);
    log_file_open := (IOResult <> 0);
    result := not log_file_open;
  end else
    result := true;
end;


procedure log (logclass : integer; msg : string);
var to_stdout, to_stderr, to_file : boolean;
begin
  to_stdout := (logclass and log_to_stdout <> 0);
  to_stderr := (logclass and log_to_stderr <> 0);
  to_file   := (logclass and log_to_logfile <> 0);
  if to_stdout then
    writeln (msg);
  if to_stderr then
    (*$IFDEF Delphi*)
    writeln (msg);   // at least delphi3 has no stderr
    (*$ELSE*)
    writeln (stderr,msg);
    (*$ENDIF*)
  if to_file then
    if log_file_open then
    begin
      Case logclass of
        log_message : Msg := 'M '+Msg;
        log_warning : Msg := 'W '+Msg;
        log_error   : Msg := 'E '+Msg;
        log_status  : Msg := 'S '+Msg;
        log_fatal   : Msg := 'F '+Msg
      else
        Msg := '  '+Msg;
      end;
      (*$IFNDEF Unix*)
      Msg := Msg + #13#10;
      (*$ELSE*)
      Msg := Msg + #10;
      (*$ENDIF*)
      BlockWrite (fLog,msg[1],length(msg));
    end;
  IOResult;
end;


procedure logfmt (logclass : integer; fmtstr : string; args : array of const);
var msg : string;
begin
  msg := format (fmtstr, args);
  log (logclass, msg);
end;

begin
  log_closefile;
end.
