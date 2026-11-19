#!/usr/bin/env python3
"""Discover publishable projects and determine independent release versions."""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

def git(*args, check=True):
    p = subprocess.run(['git', *args], cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if check and p.returncode:
        raise RuntimeError(f"git {' '.join(args)} failed: {p.stderr.strip()}")
    return p.stdout.strip() if p.returncode == 0 else ''

def properties(project):
    root = ET.parse(project).getroot()
    result = {}
    for group in root.findall('PropertyGroup'):
        for element in group:
            if element.text and element.tag not in result:
                result[element.tag] = element.text.strip()
    return result

def discover():
    packages = []
    for project in sorted((ROOT / 'src').glob('**/*.csproj')):
        meta = properties(project)
        if meta.get('IsPackable', 'true').lower() == 'false':
            continue
        name = meta.get('PackageId') or project.stem
        if not re.fullmatch(r'[A-Za-z0-9_.-]+', name):
            raise RuntimeError(f'Invalid package ID: {name}')
        test_candidates = [
            ROOT / 'tests' / f'{project.stem}.Tests' / f'{project.stem}.Tests.csproj',
            ROOT / 'src' / 'Sops.Models' / 'tests' / f'{project.stem}.Tests' / f'{project.stem}.Tests.csproj',
            ROOT / 'src' / 'Sops.Models' / 'tests' / 'Generator.Tests' / 'Generator.Tests.csproj',
        ]
        test = next((t for t in test_candidates if t.is_file()), None)
        if not test:
            raise RuntimeError(f'Missing matching test project for {name}: {test_candidates[0]}')
        packages.append({'name': name, 'project': project.relative_to(ROOT).as_posix(), 'test': test.relative_to(ROOT).as_posix()})
    names = [p['name'].lower() for p in packages]
    if len(names) != len(set(names)):
        raise RuntimeError('Duplicate package IDs')
    if not packages:
        raise RuntimeError('No publishable projects found')
    return packages

def last_tag(name):
    tags = git('tag', '--list', f'{name}/v*').splitlines()
    versions = []
    for tag in tags:
        m = re.fullmatch(re.escape(name) + r'/v(\d+)\.(\d+)\.(\d+)', tag)
        if m:
            versions.append((tuple(map(int, m.groups())), tag))
    return max(versions)[1] if versions else None

def bump(version, kind):
    major, minor, patch = version
    if kind == 'major':
        return (major + 1, 0, 0)
    if kind == 'minor':
        return (major, minor + 1, 0)
    return (major, minor, patch + 1)

def plan(packages):
    git('fetch', '--tags', '--force')
    all_paths = set(git('ls-files').splitlines())
    result = []
    for package in packages:
        name = package['name']
        tag = last_tag(name)
        prefix = package['project'].rsplit('/', 1)[0] + '/'
        testprefix = package['test'].rsplit('/', 1)[0] + '/'
        shared = ('Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', '.editorconfig', 'eng/')
        if tag:
            files = git('diff', '--name-only', f'{tag}..HEAD').splitlines()
            changed = any(f.startswith((prefix, testprefix)) or f in shared or f.startswith('eng/') for f in files)
        else:
            changed = True
        if not changed:
            continue
        if tag:
            old = tuple(map(int, tag.rsplit('/v', 1)[1].split('.')))
            subjects = git('log', '--format=%s%n%b%n==END==', f'{tag}..HEAD', '--', prefix, testprefix).split('==END==')
            kind = 'patch'
            for message in subjects:
                head = message.strip().splitlines()[0] if message.strip() else ''
                if 'BREAKING CHANGE:' in message or re.match(r'\w+(?:\([^)]*\))?!:', head):
                    kind = 'major'; break
                if re.match(r'feat(?:\([^)]*\))?:', head):
                    kind = 'minor'
            version = '.'.join(map(str, bump(old, kind)))
        else:
            version = '0.1.0'
        release_tag = f'{name}/v{version}'
        if git('rev-parse', '-q', '--verify', f'refs/tags/{release_tag}', check=False):
            raise RuntimeError(f'Tag already exists: {release_tag}')
        result.append({**package, 'version': version, 'tag': release_tag})
    return result

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['discover', 'plan'])
    parser.add_argument('--github-output')
    args = parser.parse_args()
    packages = discover()
    result = packages if args.action == 'discover' else plan(packages)
    payload = json.dumps(result, separators=(',', ':'))
    print(json.dumps(result, indent=2))
    if args.github_output:
        with open(args.github_output, 'a', encoding='utf-8') as f:
            f.write(f'matrix={payload}\n')
            f.write(f'has_packages={str(bool(result)).lower()}\n')

if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        print(f'ERROR: {exc}', file=sys.stderr)
        sys.exit(1)
