"""Version-pinned fix for LeechCore File backend concurrency.

Only changes the immediate assigned to ctxLC->fMultiThread in DeviceFile_Open.
The File backend retains its per-file locks. The core context lock now serializes
the shared contiguous-read context, as required by LcReadContigious_ReadScatterGather.
"""
import hashlib,struct,sys,pathlib,json
EXPECTED='87b69a036772e58d6d9297982f6029d02ea0dbaf98c91f366e3fbb28f6045673'
RVA=0x3c37
OLD=bytes.fromhex('c7874825000001000000')
NEW=bytes.fromhex('c7874825000000000000')
def patch(source,destination):
    original=pathlib.Path(source).read_bytes()
    if hashlib.sha256(original).hexdigest()!=EXPECTED:raise ValueError('Unrecognized LeechCore build; no patch applied')
    pe=struct.unpack_from('<I',original,0x3c)[0]
    if original[:2]!=b'MZ' or original[pe:pe+4]!=b'PE\0\0' or struct.unpack_from('<H',original,pe+4)[0]!=0x8664:raise ValueError('Not an x64 PE')
    sections=struct.unpack_from('<H',original,pe+6)[0]
    table=pe+24+struct.unpack_from('<H',original,pe+20)[0]
    offset=None
    for i in range(sections):
        entry=table+i*40
        size,virtual,rawsize,raw=struct.unpack_from('<IIII',original,entry+8)
        if virtual<=RVA<RVA+len(OLD)<=virtual+rawsize:offset=raw+RVA-virtual;break
    if offset is None or original[offset:offset+len(OLD)]!=OLD:raise ValueError('Expected DeviceFile_Open instruction not found')
    revised=original[:offset]+NEW+original[offset+len(OLD):]
    changes=[i for i,(a,b) in enumerate(zip(original,revised)) if a!=b]
    if changes!=[offset+6]:raise ValueError('Patch changes more than its designated immediate byte')
    pathlib.Path(destination).write_bytes(revised)
    report={'sourceSha256':EXPECTED,'patchedSha256':hashlib.sha256(revised).hexdigest(),'rva':hex(RVA),'fileOffset':hex(offset),'originalInstruction':OLD.hex(),'patchedInstruction':NEW.hex(),'bytesChanged':1,'scope':'LeechCore DeviceFile_Open core concurrency flag; game memory unchanged'}
    return report
if __name__=='__main__':print(json.dumps(patch(sys.argv[1],sys.argv[2]),indent=2))
