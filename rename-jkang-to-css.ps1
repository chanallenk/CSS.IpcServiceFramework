param(
    [Parameter(Mandatory=$true)]
    [string]$TargetFolder,
    
    [Parameter(Mandatory=$false)]
    [switch]$WhatIf = $false
)

# Validate target folder exists
if (-not (Test-Path $TargetFolder)) {
    Write-Error "Target folder '$TargetFolder' does not exist."
    exit 1
}

# Convert to absolute path
$TargetFolder = (Resolve-Path $TargetFolder).Path

Write-Host "Starting rename operation in: $TargetFolder" -ForegroundColor Green
if ($WhatIf) {
    Write-Host "Running in WhatIf mode - no changes will be made" -ForegroundColor Yellow
}

# Function to safely rename item
function Rename-ItemSafely {
    param(
        [string]$OldPath,
        [string]$NewPath,
        [bool]$IsWhatIf
    )
    
    if ($OldPath -eq $NewPath) {
        return $false
    }
    
    if (Test-Path $NewPath) {
        Write-Warning "Target already exists, skipping: $NewPath"
        return $false
    }
    
    if ($IsWhatIf) {
        Write-Host "WOULD RENAME: '$OldPath' -> '$NewPath'" -ForegroundColor Cyan
        return $true
    } else {
        try {
            Rename-Item -Path $OldPath -NewName (Split-Path $NewPath -Leaf) -Force
            Write-Host "RENAMED: '$OldPath' -> '$NewPath'" -ForegroundColor Green
            return $true
        } catch {
            Write-Error "Failed to rename '$OldPath': $($_.Exception.Message)"
            return $false
        }
    }
}

# Function to replace content in files
function Replace-FileContent {
    param(
        [string]$FilePath,
        [bool]$IsWhatIf
    )
    
    try {
        # Skip binary files by checking for common binary extensions
        $binaryExtensions = @('.exe', '.dll', '.pdb', '.bin', '.obj', '.lib', '.so', '.dylib', '.zip', '.7z', '.rar', '.tar', '.gz', '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx', '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.ico', '.mp3', '.mp4', '.avi', '.mov', '.wmv')
        $fileExtension = [System.IO.Path]::GetExtension($FilePath).ToLower()
        
        if ($binaryExtensions -contains $fileExtension) {
            Write-Verbose "Skipping binary file: $FilePath"
            return $false
        }
        
        # Read file content
        $content = Get-Content $FilePath -Raw -ErrorAction Stop
        
        if ($null -eq $content) {
            Write-Verbose "File is empty or couldn't be read: $FilePath"
            return $false
        }
        
        # Check if content contains "CSS" (case sensitive)
        if ($content -ccontains "CSS" -or $content -cmatch "CSS") {
            $newContent = $content -creplace "CSS", "CSS"
            
            if ($IsWhatIf) {
                $matchCount = ([regex]::Matches($content, "CSS")).Count
                Write-Host "WOULD REPLACE $matchCount occurrence(s) of 'CSS' in: $FilePath" -ForegroundColor Cyan
                return $true
            } else {
                Set-Content -Path $FilePath -Value $newContent -NoNewline -ErrorAction Stop
                $matchCount = ([regex]::Matches($content, "CSS")).Count
                Write-Host "REPLACED $matchCount occurrence(s) of 'CSS' in: $FilePath" -ForegroundColor Green
                return $true
            }
        }
        
        return $false
    } catch {
        Write-Warning "Could not process file content '$FilePath': $($_.Exception.Message)"
        return $false
    }
}

# Counters
$foldersRenamed = 0
$filesRenamed = 0
$filesContentChanged = 0

# Step 1: Rename folders (process from deepest to shallowest to avoid path issues)
Write-Host "`nStep 1: Renaming folders..." -ForegroundColor Yellow

$folders = Get-ChildItem -Path $TargetFolder -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending

foreach ($folder in $folders) {
    if ($folder.Name -ccontains "CSS" -or $folder.Name -cmatch "CSS") {
        $newName = $folder.Name -creplace "CSS", "CSS"
        $newPath = Join-Path $folder.Parent.FullName $newName
        
        if (Rename-ItemSafely -OldPath $folder.FullName -NewPath $newPath -IsWhatIf $WhatIf) {
            $foldersRenamed++
        }
    }
}

# Step 2: Rename files
Write-Host "`nStep 2: Renaming files..." -ForegroundColor Yellow

# Re-scan after folder renames (if not in WhatIf mode)
if (-not $WhatIf) {
    $files = Get-ChildItem -Path $TargetFolder -Recurse -File
} else {
    $files = Get-ChildItem -Path $TargetFolder -Recurse -File
}

foreach ($file in $files) {
    if ($file.Name -ccontains "CSS" -or $file.Name -cmatch "CSS") {
        $newName = $file.Name -creplace "CSS", "CSS"
        $newPath = Join-Path $file.Directory.FullName $newName
        
        if (Rename-ItemSafely -OldPath $file.FullName -NewPath $newPath -IsWhatIf $WhatIf) {
            $filesRenamed++
        }
    }
}

# Step 3: Replace content in files
Write-Host "`nStep 3: Replacing content in files..." -ForegroundColor Yellow

# Re-scan files after renames (if not in WhatIf mode)
if (-not $WhatIf) {
    $files = Get-ChildItem -Path $TargetFolder -Recurse -File
} else {
    $files = Get-ChildItem -Path $TargetFolder -Recurse -File
}

foreach ($file in $files) {
    if (Replace-FileContent -FilePath $file.FullName -IsWhatIf $WhatIf) {
        $filesContentChanged++
    }
}

# Summary
Write-Host "`n=== SUMMARY ===" -ForegroundColor Magenta
if ($WhatIf) {
    Write-Host "WhatIf Mode - No actual changes made:" -ForegroundColor Yellow
    Write-Host "  Would rename $foldersRenamed folder(s)" -ForegroundColor Cyan
    Write-Host "  Would rename $filesRenamed file(s)" -ForegroundColor Cyan
    Write-Host "  Would modify content in $filesContentChanged file(s)" -ForegroundColor Cyan
    Write-Host "`nTo perform actual changes, run the script without -WhatIf parameter" -ForegroundColor Yellow
} else {
    Write-Host "Operation completed:" -ForegroundColor Green
    Write-Host "  Renamed $foldersRenamed folder(s)" -ForegroundColor Green
    Write-Host "  Renamed $filesRenamed file(s)" -ForegroundColor Green
    Write-Host "  Modified content in $filesContentChanged file(s)" -ForegroundColor Green
}

Write-Host "`nOperation finished." -ForegroundColor Green
