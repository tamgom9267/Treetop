# Fase 4: cuartos, slimes y loot

## Integracion

La sesion existente carga `Local/MultiplayerPlayers`, que ahora incluye `MultiplayerWorldSession`, `MultiplayerLootSession` y `MultiplayerInventoryAdapter`. No hay componentes nuevos que asignar a escenas.

HostWorldGenerator y ClientWorldGenerator cargan los recursos de su rol mediante constantes, para que las rutas antiguas serializadas en la escena no prevalezcan. Las puertas, paredes, puentes y portal conservan sus recursos anteriores.

Recursos nuevos:

- Host/Cuartos y Client/Cuartos: 0 a 8, boss1 y boss2.
- Host/Enemigos y Client/Enemigos: Enemigo1 y Enemigo2.
- Local/Loot: copias de los prefabs con InventoryObject detectados por la herramienta; actualmente sword.

Los host conservan los datos NavMesh originales como referencias de lectura. Los clientes no tienen componentes de navegacion. Los cuartos que originalmente no tienen NavMesh siguen sin tenerlo, incluido el cuarto 7 con sus spawners.

## Inventario: agregado, modificado y eliminado

**Archivos originales del inventario: ninguno modificado o eliminado.** PlayerInventory, PlayerController, WeaponObject, InventoryObject y CursorObject conservan su implementacion y sus prefabs originales.

Se agregaron una identidad de red independiente del ID de la cuadrilla, un catalogo de prefabs de loot, un registro transaccional en el host y un adaptador local. El host resuelve la primera solicitud valida que recibe; no se utiliza el reloj del cliente para decidir quien llego primero.

El adaptador consume la interaccion antes del controlador original. Solo abre la ventana con el objeto reservado cuando llega la confirmacion del host. Conserva los controles y paneles existentes. Al guardar, tirar o cerrar, informa los cambios del objeto real; no transmite la copia equipada en Hand. Una confirmacion retrasada de guardar no cancela un intento posterior de soltar el objeto.

Para descartar una copia se desequipa, se retira de Inventory y Loot, se limpian ambas cuadrillas, slots, seleccion, proximidad, cursor y paneles, y solo despues se destruye. El ID de UI se devuelve una sola vez. Si se cancela un arrastre, se restaura su disposicion previa. Una forma rotada se vuelve a orientar cuando hace falta para caber en el panel de loot.

La reflexion esta limitada a PlayerInventory.isMoving, PlayerInventory.SpriteObject y PlayerController.receivedHitStopDuration. Se valida el tipo de esos campos y link.xml los conserva en compilaciones que aplican stripping. La duracion anterior del hit stop se restaura al terminar la sesion.

Antes de reconstruir un piso hay una barrera fiable: todos cierran inventario, resuelven las solicitudes pendientes y confirman. Los objetos confirmados del inventario conservan identidad entre pisos. Los del suelo anterior se retiran. Si se desconecta un cliente, el host libera sus reservas; localmente solo se conservan las adquisiciones confirmadas y los objetos que ya traia desde la aldea. Una recogida o guardado sin confirmacion no se convierte en propiedad persistente por una desconexion.

## Autoridad y eventos

- Presencia: se comprueba la interseccion real del collider local con el trigger del piso, cada 250 ms. El host exige a todos los participantes actuales y descarta presencia sin actualizar durante 1.5 segundos.
- Cuartos: esperando, activo y completado. Se cierran las dos puertas, se generan enemigos una sola vez y se abren al concluir. Un cuarto sin enemigos se completa directamente.
- Slimes: solo HostSlimeMovement consulta NavMesh y aplica fuerzas. El host escoge al jugador mas cercano presente en el cuarto.
- Clientes: Rigidbody cinematico, interpolacion de 100 ms y estados a 15 Hz. Los Animation Events de salto y eliminacion son pasivos en las replicas.
- Ataques: identidad de jugador, ataque, arma y enemigo, vigencia y distancia. Solo un impacto de cada ataque por enemigo; golpes de dos jugadores son independientes. El host confirma salud y knockback. Las replicas no suman otro impulso.
- Da?o por contacto y salud del personaje: locales, conservando el HUD. Dar y recibir golpes de los slimes nuevos no hace hit stop. La pausa sigue usando el comportamiento existente.
- Eventos 30-33: mundo, enemigos y snapshots. Eventos 40-43: loot, snapshots y barrera de cambio de piso. Los eventos de jugadores de fase 3 permanecen en 20-23.
- Todos los mensajes de gameplay incluyen version, partida y piso. Los estados de entidades tienen revision; solo se aceptan confirmaciones del host original. Se pide un snapshot cada tres segundos para recuperar el estado vigente.

## Herramientas y pruebas

Fuera de Play, usar Tools > Multiplayer:

1. Crear prefabs de fase 4: crea los recursos que falten y configura el coordinador. No guarda escenas. Las copias existentes de cuartos y enemigos se conservan, para permitir ajustes manuales.
2. Validar prefabs de fase 4: comprueba roles, spawners, navegacion y componentes.
3. Probar fase 4 (sin red): ejecuta pruebas nativas sobre presencia, puertas, ataques, adjudicacion y retirada de inventario, ademas de las pruebas de fase 3. Usa escenas de vista previa sin guardar archivos de escena.

Los informes se escriben en Temp/phase4-verification/editor-tests.json y Temp/phase3-verification/editor-tests.json. Las pruebas sin red no sustituyen una partida real con varias instancias.

## Prueba real con dos y tres instancias

Todas deben usar esta misma version de scripts y prefabs, la configuracion Photon existente y comenzar desde la aldea.

1. Crear la party, unir un cliente y pulsar Listo. Entrar con una seed valida y comprobar spawns y equipo previo.
2. Enviar solo un jugador al cuarto siguiente: no debe activarse. Entrar con el otro: cerrar puertas y generar el mismo numero y tipo de slimes en ambos equipos.
3. Probar ataques desde cada rol, golpes simultaneos, muerte y apertura sincronizada. Verificar color y knockback sin congelar animaciones. Los clientes no deben calcular caminos.
4. Tirar una espada: debe aparecer una copia en cada equipo con la misma ubicacion. Intentar abrir su loot simultaneamente: solo un jugador obtiene la reserva.
5. Equiparla desde loot, guardarla, volver a tirarla y recogerla con el otro jugador. Probar varias espadas iguales y una espada previamente rotada. Ningun inventario ni Hand debe conservar copias perdedoras.
6. Cerrar el loot sin guardar: debe reaparecer en el suelo. Desconectar al jugador que solo la estaba inspeccionando: el host debe liberarla cerca de su ultima posicion conocida.
7. Cambiar de piso con inventario abierto, un arrastre activo o una solicitud pendiente. Deben cerrarse y confirmarse antes de reconstruir; conservar objetos confirmados y retirar los del suelo.
8. Repetir con un tercer integrante: el cuarto exige a los tres. Si sale un cliente, los dos restantes pasan a ser el grupo requerido. La desconexion del host mantiene el regreso a la aldea de las fases anteriores.
9. Volver al solitario y comprobar controles, inventario, equipamiento y hit stop original. No es necesario configurar nuevos objetos de escena.
