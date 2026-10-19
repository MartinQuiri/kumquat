## Qué cambia y por qué

<!-- Una o dos frases. Enlaza el issue si existe: Closes #123 -->

## Cómo se probó

- [ ] Tests nuevos o actualizados (unitarios / integración por arista / E2E)
- [ ] `dotnet format --verify-no-changes` limpio
- [ ] Si agrega un motor o binario: entrada en `tools/engines.json` con SHA-256 y licencia, y `THIRD-PARTY-NOTICES.md` actualizado
- [ ] Si toca la rueda o el arrastre: probado a mano con Explorer (ver `docs/TESTING-MANUAL.md`)

## Checklist de seguridad

- [ ] Ningún proceso externo se lanza fuera de `ProcessRunner`
- [ ] Ninguna ruta del usuario llega a un motor sin prefijo/escape (p. ej. `file:` en FFmpeg)
- [ ] No se registran pulsaciones de teclado ni contenido de archivos
