[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:Session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$script:BaseUrl = $null
$securePassword = $null
$passwordPointer = [IntPtr]::Zero
$plainPassword = $null
$loginBody = $null

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Path,
        [string]$Body,
        [hashtable]$Headers,
        [int[]]$ExpectedStatusCodes = @(200)
    )

    $requestParameters = @{
        Uri             = "$($script:BaseUrl)$Path"
        Method          = $Method
        WebSession      = $script:Session
        UseBasicParsing = $true
        ErrorAction     = 'Stop'
    }

    if (-not [string]::IsNullOrWhiteSpace($Body)) {
		$requestParameters.Body = $Body
		$requestParameters.ContentType = 'application/json; charset=utf-8'
	}

    if ($null -ne $Headers) {
        $requestParameters.Headers = $Headers
    }

    try {
        $response = Invoke-WebRequest @requestParameters
        $statusCode = [int]$response.StatusCode
    }
    catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) {
            throw "Request $Method $Path failed without an HTTP response. Detail: $($_.Exception.Message)"
        }

        try {
            $statusCode = [int]$errorResponse.StatusCode
        }
        catch {
            throw "Request $Method $Path returned an unreadable HTTP status."
        }

        if ($ExpectedStatusCodes -notcontains $statusCode) {
            throw "Unexpected HTTP status $statusCode for $Method $Path."
        }

        return $null
    }

    if ($ExpectedStatusCodes -notcontains $statusCode) {
        throw "Unexpected HTTP status $statusCode for $Method $Path."
    }

    if ([string]::IsNullOrWhiteSpace($response.Content)) {
        return $null
    }

    try {
        return $response.Content | ConvertFrom-Json
    }
    catch {
        throw "Response from $Method $Path was not valid JSON."
    }
}

function Get-CsrfToken {
    $csrf = Invoke-ApiRequest -Method 'GET' -Path '/api/v1/auth/csrf'
    if ($null -eq $csrf -or
        [string]::IsNullOrWhiteSpace([string]$csrf.requestToken) -or
        [string]::IsNullOrWhiteSpace([string]$csrf.headerName)) {
        throw 'The CSRF response did not contain the required fields.'
    }

    if ($csrf.headerName -ne 'X-CSRF-TOKEN') {
        throw 'The API returned an unexpected CSRF header name.'
    }

    return [pscustomobject]@{
        Token      = [string]$csrf.requestToken
        HeaderName = [string]$csrf.headerName
    }
}


function New-CsrfHeaders {
    param(
        [Parameter(Mandatory)]
        [psobject]$Csrf
    )

    $headers = @{}
    $headers[$Csrf.HeaderName] = $Csrf.Token
    return $headers
}

try {
    $enteredBaseUrl = (Read-Host 'API URL (por ejemplo, https://localhost:5001)').Trim().TrimEnd('/')
    $parsedBaseUrl = $null
    if (-not [Uri]::TryCreate($enteredBaseUrl, [UriKind]::Absolute, [ref]$parsedBaseUrl) -or
        $parsedBaseUrl.Scheme -notin @('http', 'https')) {
        throw 'The API URL must be an absolute HTTP or HTTPS URL.'
    }
    $script:BaseUrl = $enteredBaseUrl

    $adminEmail = (Read-Host 'Correo del administrador de desarrollo').Trim()
    if ([string]::IsNullOrWhiteSpace($adminEmail)) {
        throw 'The administrator email is required.'
    }

    $securePassword = Read-Host 'Contraseña del administrador de desarrollo' -AsSecureString
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)

    $csrf = Get-CsrfToken
    Write-Host 'OK GET /api/v1/auth/csrf (200)'

    $loginBody = @{
        userName = $adminEmail
        password = $plainPassword
    } | ConvertTo-Json -Compress
    $login = Invoke-ApiRequest `
        -Method 'POST' `
        -Path '/api/v1/auth/login' `
        -Headers (New-CsrfHeaders $csrf) `
        -Body $loginBody
    if ($null -eq $login -or $login.authenticated -ne $true) {
        throw 'The login response did not confirm authentication.'
    }
    Write-Host 'OK POST /api/v1/auth/login (200)'

    $meBeforeTenant = Invoke-ApiRequest -Method 'GET' -Path '/api/v1/auth/me'
    if ($null -eq $meBeforeTenant -or $null -ne $meBeforeTenant.tenant) {
        throw 'The authenticated user unexpectedly has a selected tenant before selection.'
    }
    Write-Host 'OK GET /api/v1/auth/me before tenant selection (200)'

    $csrf = Get-CsrfToken
    Write-Host 'OK GET /api/v1/auth/csrf after login (200)'

    $selectTenantBody = @{ tenantCode = 'ubimia-dev' } | ConvertTo-Json -Compress
    $selection = Invoke-ApiRequest `
        -Method 'POST' `
        -Path '/api/v1/auth/select-tenant' `
        -Headers (New-CsrfHeaders $csrf) `
        -Body $selectTenantBody
    Write-Host 'OK POST /api/v1/auth/select-tenant (200)'

    $meAfterTenant = Invoke-ApiRequest -Method 'GET' -Path '/api/v1/auth/me'
    if ($null -eq $meAfterTenant -or
        $null -eq $meAfterTenant.tenant -or
        $meAfterTenant.tenant.tenantCode -ne 'ubimia-dev') {
        throw 'The selected tenant was not returned by /api/v1/auth/me.'
    }

    $permissions = @($meAfterTenant.permissions)
    if ($permissions -notcontains 'contracts.read') {
        throw 'The selected tenant does not contain contracts.read.'
    }
    Write-Host 'OK GET /api/v1/auth/me after tenant selection (200)'

    $csrf = Get-CsrfToken
    $contractNumber = [Environment]::GetEnvironmentVariable('UCREDIT_TEST_CONTRACT')
    if ([string]::IsNullOrWhiteSpace($contractNumber)) {
        $contractNumber = Read-Host 'Numero de contrato [454890CD]'
        if ([string]::IsNullOrWhiteSpace($contractNumber)) {
            $contractNumber = '454890CD'
        }
    }
    $contractNumber = $contractNumber.Trim()
    if ([string]::IsNullOrWhiteSpace($contractNumber) -or $contractNumber.Length > 15) {
        throw 'The contract number is required and must not exceed 15 characters.'
    }

    $encodedContractNumber = [Uri]::EscapeDataString($contractNumber)
    $contractDetail = Invoke-ApiRequest `
        -Method 'GET' `
        -Path "/api/v1/contracts/$encodedContractNumber"
    if ($null -eq $contractDetail -or
        [string]$contractDetail.contractNumber -cne $contractNumber) {
        throw 'The contract detail did not return the requested contract number exactly.'
    }
    Write-Host 'OK GET /api/v1/contracts/{contractNumber} (200; exact contract confirmed)'

    $contractSearch = Invoke-ApiRequest `
        -Method 'GET' `
        -Path "/api/v1/contracts?contractNumber=$encodedContractNumber&page=1&pageSize=10"
    if ($null -eq $contractSearch -or $null -eq $contractSearch.items) {
        throw 'The contract search response did not contain results.'
    }

    $contractItems = @($contractSearch.items)
    if ($contractItems.Count -lt 1) {
        throw 'The exact contract search returned no results.'
    }

    $seenContractNumbers = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($contractItem in $contractItems) {
        $returnedContractNumber = [string]$contractItem.contractNumber
        if ($returnedContractNumber -cne $contractNumber) {
            throw 'The exact contract search returned a different contract number.'
        }
        if (-not $seenContractNumbers.Add($returnedContractNumber)) {
            throw 'The exact contract search returned duplicate contract numbers.'
        }
    }
    Write-Host 'OK GET /api/v1/contracts?contractNumber=... (200; exact results confirmed)'

    Write-Host 'OK GET /api/v1/auth/csrf before logout (200)'

    Invoke-ApiRequest `
        -Method 'POST' `
        -Path '/api/v1/auth/logout' `
        -Headers (New-CsrfHeaders $csrf) `
        -ExpectedStatusCodes @(204)
    Write-Host 'OK POST /api/v1/auth/logout (204)'

    Invoke-ApiRequest `
        -Method 'GET' `
        -Path '/api/v1/auth/me' `
        -ExpectedStatusCodes @(401)
    Write-Host 'OK GET /api/v1/auth/me after logout (401)'

    Write-Host 'Local Identity smoke test completed successfully.'
}
catch {
    Write-Error ("Local Identity smoke test failed: " + $_.Exception.Message)
    exit 1
}
finally {
    if ($passwordPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    }
    if ($null -ne $securePassword) {
        $securePassword.Dispose()
    }
    $plainPassword = $null
    $loginBody = $null
    $contractNumber = $null
    $encodedContractNumber = $null
}
