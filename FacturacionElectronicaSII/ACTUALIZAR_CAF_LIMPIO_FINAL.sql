-- Actualizar CAF con XML limpio proporcionado por el usuario
-- Fecha: 2026-01-19
-- Tipo DTE: 33
-- Rango: 62-90

SET FOREIGN_KEY_CHECKS = 0;

UPDATE CAF 
SET 
    CAFContenido = '<?xml version="1.0"?>
<AUTORIZACION>
<CAF version="1.0">
<DA>
<RE>8451335-0</RE>
<RS>MARTA INALBIA URRA ESCOBAR</RS>
<TD>33</TD>
<RNG><D>62</D><H>90</H></RNG>
<FA>2026-01-19</FA>
<RSAPK><M>vVPbpja9b8r1kFn2SqI6A4xCyk823Hw3kTY9hUsJfB+WvP86G8egx9SvsGzxgC4KXG1mzhrD5sYzxMEFd1hTtQ==</M><E>Aw==</E></RSAPK>
<IDK>100</IDK>
</DA>
<FRMA algoritmo="SHA1withRSA">FuOX7aAU8JgmW9k7ul8+ExK9klxqYJjgfbsyNs27zTt2+8yc+ZasL83tQ/oChLbrlv1QrZsN7EfsOn9VDp26vQ==</FRMA>
</CAF>
<RSASK>-----BEGIN RSA PRIVATE KEY-----
MIIBOgIBAAJBAL1T26Y2vW/K9ZBZ9kqiOgOMQspPNtx8N5E2PYVLCXwflrz/OhvH
oMfUr7Bs8YAuClxtZs4aw+bGM8TBBXdYU7UCAQMCQH4358QkfkqHTmA7+YcW0Vey
1zGKJJL9emDO064yBlK+ksiHHJPigW3cHirehsc5FMDw7FzoISW6lYeIfkVmu7MC
IQDzl1GqkXMYsh99LsxKi82yRAER7ojz8RaK4MJx6N35FwIhAMb44uSsgMXw6wVB
UtzJirj3AvJUNZ49F8iYsdYmYEETAiEAomThHGD3ZcwU/h8y3F0zzC1WC/RbTUtk
XJXW9ps+pg8CIQCEpeyYcwCD9fIDgOHohlx7T1dMOCO+02UwZcvkGZWAtwIhAMvr
u+jb1f+G6YxJqosgt/2lhFVVDOqWz8zz3ARFztvV
-----END RSA PRIVATE KEY-----
</RSASK>

<RSAPUBK>-----BEGIN PUBLIC KEY-----
MFowDQYJKoZIhvcNAQEBBQADSQAwRgJBAL1T26Y2vW/K9ZBZ9kqiOgOMQspPNtx8
N5E2PYVLCXwflrz/OhvHoMfUr7Bs8YAuClxtZs4aw+bGM8TBBXdYU7UCAQM=
-----END PUBLIC KEY-----
</RSAPUBK>
</AUTORIZACION>',
    RangoInicio = 62,
    RangoFin = 90,
    FechaCarga = '2026-01-19 00:00:00',
    FRMA = 'FuOX7aAU8JgmW9k7ul8+ExK9klxqYJjgfbsyNs27zTt2+8yc+ZasL83tQ/oChLbrlv1QrZsN7EfsOn9VDp26vQ=='
WHERE TD = 33 AND Estado = 'Activo';

SET FOREIGN_KEY_CHECKS = 1;

-- Verificar que se actualizó correctamente
SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    LENGTH(CAFContenido) as LongitudXML,
    LEFT(CAFContenido, 200) as Primeros200Caracteres
FROM CAF 
WHERE TD = 33 AND Estado = 'Activo';
