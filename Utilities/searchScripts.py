import argparse
import os
import re
import sys
from shutil import copyfile, move

DIR_SCRIPTS = 'scripts'

SCENEINFO = 'sceneinfo.txt'
EXT_MAT = '.mat'
EXT_TXT = '.txt'
PTRN_SCENE = re.compile(r'\s+"sceneName": "(.*)",?')

def clearDirectory(absPath):
  if args.test:
    print( 'Remove: {}'.format(absPath) )
  else:
    for item in os.listdir(absPath):
      os.remove(absPath + item)
    # Now the directory is empty of files
    os.rmdir(absPath)

def assemblePathStr(scriptName, ext):
  if args.spl:
    # If simple mode, file extension also exists.
    # Remove the extension
    scriptName_noExt = scriptName[:scriptName.index('.')]
    return (args.dir_rdr + scriptName_noExt + ext)
  
  return args.dir_rdr + DIR_SCRIPTS + '/' + scriptName + ext


def isScriptInInterest(scriptName):
  # For simple mode, .mat files are also included but we don't care about them.
  # return false in this case
  if scriptName.find('.mat') != -1:
    return False

  with open(assemblePathStr(scriptName, EXT_TXT)) as f:
    for line in f:
      if args.srpt:
        for s in args.srpt:
          if s in line:
            return True
      else:
        if args.srpt_match in line:
          return True

  return False
        
def handleScript(scriptName):

  # Move .mat file to the original directory if exists
  ignoreMatFile = False
  oldPath = args.dir_rdr + scriptName + '/' + scriptName + EXT_MAT
  # .mat file can be either 'scripts' folder or 'scripts_gen_XXXXXX' folder
  if not os.path.exists(oldPath):
    oldPath = assemblePathStr(scriptName, EXT_MAT)
    if not os.path.exists(oldPath):
      ignoreMatFile = True
  if not ignoreMatFile:
    newPath = 1
    if args.spl:
      newPath = args.dir_srpt + scriptName[:scriptName.index('.')] + EXT_MAT
    else:
      newPath = args.dir_srpt + scriptName + EXT_MAT
    fileOperation(oldPath, newPath)

  # Move script to the original directory
  oldPath = assemblePathStr(scriptName, EXT_TXT)
  if args.spl:
    newPath = args.dir_srpt + scriptName[:scriptName.index('.')] + EXT_TXT
  else:
    newPath = args.dir_srpt + scriptName + EXT_TXT
  fileOperation(oldPath, newPath)
  
  # Do not remove rendered data if simple mode - such folder won't exist anyway
  if not args.spl:
    clearDirectory(args.dir_rdr + scriptName + '/')
  print('Processed ' + scriptName)

def fileOperation(oldPath, newPath):
  if args.test:
    print('From:' + oldPath)
    print('To:' + newPath)
  elif args.cpy:
    copyfile(oldPath, newPath)
  else:
    move(oldPath, newPath)

def isSceneInInterest(scriptName):
  with open(args.dir_rdr + scriptName + '/' + SCENEINFO, 'r') as f:
    for line in f:
      if line[0] != '{' and line[0] != '}':
        result = PTRN_SCENE.match(line)
        if result and result.group(1) in args.scene:
          return True

  return False
        
#=================================== Main clde ===================================
parser = argparse.ArgumentParser()

# Required
parser.add_argument('dir_rdr', help='Directory where renderings are stored')
parser.add_argument('dir_srpt', help='Directory where matching scripts will be moved to')
parser.add_argument('srpt_match', help='String pattern that will be used to find scripts in interest')

# Optional - additional arguments
parser.add_argument('-srpt', help='If there is more than one string you need to match to find scripts.', action='append')
parser.add_argument('-scene', help='If there are some scenes you need to match, specify them one by one (e.g. -scene Scene_5 -scene Scene_0).', action='append')

# OPtional - execution modes
parser.add_argument('-test', help='Test Mode: It will output actions without actually performing them.', action='store_true')
parser.add_argument('-spl', help='Simple Mode: Simply move the matching scripts in a given directory. Does not remove rendered data', action='store_true')
parser.add_argument('-cpy', help='Copy Mode: Default moves the scripts from original path. This mode make a copy instead.', action='store_true')

args = parser.parse_args()

if not args.dir_rdr.endswith('/'):
  args.dir_rdr += '/'
if not args.dir_srpt.endswith('/'):
  args.dir_srpt += '/'

if not os.path.exists(args.dir_srpt):
  os.makedirs(args.dir_srpt)

# Merge so that we only have one variable to store all string matches
if args.srpt:
  args.srpt.append(args.srpt_match)


cntr = 0
for scriptName in os.listdir(args.dir_rdr + DIR_SCRIPTS):
  if scriptName[0] != '.':
    scriptName_noExt = scriptName[:scriptName.index('.')]

    # if scene is specified, find if this is rendered on the specified scene
    if args.scene:
      if isSceneInInterest(scriptName_noExt) and isScriptInInterest(scriptName_noExt):
        handleScript(scriptName_noExt)
        cntr+=1
    elif isScriptInInterest(scriptName_noExt):
      handleScript(scriptName_noExt)
      cntr+=1

print('\nProcessed ' + str(cntr) + ' scripts')

