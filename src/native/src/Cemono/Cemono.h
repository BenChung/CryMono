#pragma once

#include "Headers/ICemono.h"
#include "MonoPathUtils.h"

#include <CryExtension/Impl/ClassWeaver.h>
#include <mono/jit/jit.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/appdomain.h>
#include <mono/metadata/object.h>
#include <mono/metadata/threads.h>
#include <mono/metadata/environment.h>


class CCemono : public ICemono
{
	CRYINTERFACE_BEGIN()
	CRYINTERFACE_ADD(ICemono)
	CRYINTERFACE_END()
	
	CRYGENERATE_SINGLETONCLASS(CCemono, "Cemono", 0xc37b8ad5d62f47de, 0xa8debe525ff0fc8a)

public:
	// ICemono interface
	virtual bool Init();
	virtual void Shutdown();
	virtual void AddClassBinding(ICemonoClassBinding* pBinding);
	// -ICemono


	virtual void GetMemoryStatistics(ICrySizer * s) const;

	// Statics
	static string ToString(MonoString* monoString)
	{
		return mono_string_to_utf8(monoString);
	}

	static MonoString* ToMonoString(const char* string)
	{
		return mono_string_new(mono_domain_get(), string);
	}


private:
	bool InitializeDomain();
	void RegisterDefaultBindings();
	bool InitializeBaseClassLibraries();
	bool InitializeManager();

	MonoDomain* m_pMonoDomain;
	MonoAssembly* m_pManagerAssembly;
	MonoAssembly* m_pBclAssembly;
	MonoImage* m_pBclImage;
	MonoObject* m_pManagerObject;

	bool m_bDebugging;
	std::vector<ICemonoClassBinding*>  m_classBindings;

};